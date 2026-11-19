#!/usr/bin/env python3
"""The diagrams of docs/ARCHITECTURE.md, and the numbers it gives.

    python3 docs/architecture/generate.py           writes every docs/architecture/<n>-<view>.svg
    python3 docs/architecture/generate.py --count   prints the numbers the document gives

Only the Python 3 standard library. Each diagram is one block below that places its elements by hand,
with coordinates, so that a diagram is text: a change to it is a changed line in a pull request, and
they all look alike. The helper (Diagram) draws group boxes, cards, people, stores and labeled
arrows between named elements, in two accent colors that mean the same thing in every diagram:

    blue  (--arch-main) the main path: what the user asked for, a read, a write
    amber (--arch-side) background work, events, and what is optional

and dashed outlines for what is optional or outside gmd. The SVGs use currentColor for text and
strokes and CSS variables with light fallbacks for fills, so they render on GitHub and in an image
viewer, and follow a page's theme when inlined in one.
"""

import os
import re
import sys
from xml.sax.saxutils import escape

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "docs", "architecture")

# ------------------------------------------------------------------------------------------------
# Counting. How each number is counted is said in docs/ARCHITECTURE.md, "Regenerating the diagrams".
# ------------------------------------------------------------------------------------------------

# Vendored code, counted apart from gmd's own
VENDORED = [os.path.join("gmd", "Utils", "GlobPatterns")]

# The modules of the product, as the code structure view draws them: a folder of gmd/, and the
# augmented layer apart from the rest of Server/, since it is a layer of its own
MODULES = [
    ("Cui", "gmd/Cui"),
    ("Server", "gmd/Server"),
    ("Augmented", "gmd/Server/Private/Augmented"),
    ("Git", "gmd/Git"),
    ("Utils", "gmd/Utils"),
    ("Common", "gmd/Common"),
    ("Installation", "gmd/Installation"),
    ("Program", "gmd"),  # the files in gmd/ itself: Program.cs, Build.cs, Usings.cs
]


def source_files(folder, recursive=True):
    """The .cs files under a folder of the repository, without obj/ and bin/ and vendored code."""
    top = os.path.join(ROOT, folder)
    for dirpath, dirnames, filenames in os.walk(top):
        rel = os.path.relpath(dirpath, ROOT)
        dirnames[:] = sorted(
            d for d in dirnames if d not in ("obj", "bin") and os.path.join(rel, d) not in VENDORED
        )
        for f in sorted(filenames):
            if f.endswith(".cs"):
                yield os.path.join(dirpath, f)
        if not recursive:
            break


def line_count(path):
    with open(path, encoding="utf-8-sig") as f:
        return sum(1 for _ in f)


def module_lines(name, folder):
    if name == "Program":
        files = list(source_files(folder, recursive=False))
    else:
        files = list(source_files(folder))
        if name == "Server":  # without the augmented layer, counted on its own
            aug = os.path.join(ROOT, "gmd", "Server", "Private", "Augmented")
            files = [f for f in files if not f.startswith(aug + os.sep)]
    return sum(line_count(f) for f in files), len(files)


TEST_METHOD = re.compile(r"^\s*\[(TestMethod|DataTestMethod)\b")
DATA_ROW = re.compile(r"^\s*\[DataRow\b")
CLASS_DECL = re.compile(r"\bclass\s+\w+")
INTEGRATION = '[TestCategory("Integration")]'


def test_counts(project):
    """Test declarations ([TestMethod]) of a test project, and how many of them are Integration: in a
    class marked so, or marked so themselves."""
    methods = integration = data_rows = 0
    for path in source_files(project):
        with open(path, encoding="utf-8-sig") as f:
            lines = f.readlines()
        attributes = []  # the attribute lines above the declaration being read
        is_class_integration = False
        pending_method = False
        for line in lines:
            stripped = line.strip()
            if stripped.startswith("["):
                attributes.append(stripped)
                if TEST_METHOD.match(line):
                    pending_method = True
                if DATA_ROW.match(line):
                    data_rows += 1
                continue
            if stripped.startswith("//") or stripped == "":
                continue
            if CLASS_DECL.search(stripped) and not pending_method:
                is_class_integration = any(INTEGRATION in a for a in attributes)
            elif pending_method:
                methods += 1
                if is_class_integration or any(INTEGRATION in a for a in attributes):
                    integration += 1
            attributes = []
            pending_method = False
    return methods, integration, data_rows


def counts():
    modules = {name: module_lines(name, folder) for name, folder in MODULES}
    product = sum(lines for lines, _ in modules.values())
    product_files = sum(files for _, files in modules.values())
    vendored = sum(line_count(f) for v in VENDORED for f in source_files(v))
    unit = test_counts("gmdTest")
    e2e = test_counts("gmdE2eTest")
    test_lines = sum(line_count(f) for p in ("gmdTest", "gmdE2eTest") for f in source_files(p))
    with open(os.path.join(ROOT, "CHANGELOG.md"), encoding="utf-8") as f:
        releases = sum(1 for line in f if line.startswith("## [v"))
    with open(os.path.join(ROOT, "gmd", "gmd.csproj"), encoding="utf-8") as f:
        packages = [
            m.group(1)
            for m in re.finditer(r'<PackageReference Include="([^"]+)"', f.read())
            if not m.group(1).startswith("CSharpier")
        ]
    # Two facts the inference view states, read from the code so that the picture follows it
    aug = os.path.join(ROOT, "gmd", "Server", "Private", "Augmented", "Private")
    with open(os.path.join(aug, "CommitBranchService.cs"), encoding="utf-8-sig") as f:
        rules = len(set(re.findall(r"rules\.(Try\w+)\(", f.read())))
    with open(os.path.join(aug, "AugmentedService.cs"), encoding="utf-8-sig") as f:
        max_commits = int(re.search(r"MaxCommitCount = (\d+);", f.read()).group(1))
    return {
        "rules": rules,
        "max_commits": max_commits,
        "modules": modules,
        "product": product,
        "product_files": product_files,
        "vendored": vendored,
        "unit": unit,
        "e2e": e2e,
        "test_lines": test_lines,
        "releases": releases,
        "packages": packages,
    }


def about(n):
    """A number as the diagrams write it: two significant figures, '34k', '6.1k', '790'."""
    if n >= 10000:
        return f"{round(n / 1000)}k"
    if n >= 1000:
        return f"{round(n / 1000, 1):g}k"
    if n >= 100:
        return f"{round(n, -1)}"
    return f"{n}"


def print_counts(c):
    print("Product code: lines of the .cs files under gmd/, without obj/, bin/ and vendored code")
    for name, (lines, files) in c["modules"].items():
        print(f"  {name:<13}{lines:>7} lines  {files:>4} files   (about {about(lines)})")
    print(f"  {'total':<13}{c['product']:>7} lines  {c['product_files']:>4} files   (about {about(c['product'])})")
    print(f"  vendored     {c['vendored']:>7} lines   (gmd/Utils/GlobPatterns, not in the total)")
    methods, integration, rows = c["unit"]
    print("Tests: [TestMethod] declarations; [DataRow]s are not counted, each runs its method once more")
    print(f"  gmdTest      {methods:>7} declared, {integration} of them Integration, so {methods - integration} fast; {rows} data rows")
    methods, _, rows = c["e2e"]
    print(f"  gmdE2eTest   {methods:>7} declared, all E2e (and Integration); {rows} data rows")
    print(f"  test code    {c['test_lines']:>7} lines of the .cs files under gmdTest/ and gmdE2eTest/")
    print(f"Releases: {c['releases']} versions in CHANGELOG.md ('## [v' headings)")
    print(f"Inference: {c['rules']} rules in CommitBranchService.DetermineCommitBranch; the log read is {c['max_commits']:,} commits")
    print(f"Packages: {len(c['packages'])} in gmd/gmd.csproj besides CSharpier: {', '.join(c['packages'])}")


# ------------------------------------------------------------------------------------------------
# The diagram helper
# ------------------------------------------------------------------------------------------------

FONT = "-apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif"
ACCENT = {None: "currentColor", "main": "var(--arch-main, #0969da)", "side": "var(--arch-side, #b07800)"}
TITLE_SIZE, LINE_SIZE, LABEL_SIZE = 14, 12.5, 11.5
LINE_HEIGHT = 16


class Diagram:
    def __init__(self, name, title, width, height):
        self.name, self.title, self.width, self.height = name, title, width, height
        self.boxes = {}  # element name -> (x, y, w, h)
        self.back = []  # groups, drawn first
        self.middle = []  # arrows, under the cards
        self.front = []  # cards and labels

    # --- elements --------------------------------------------------------------------------------

    def group(self, x, y, w, h, label, dashed=False):
        """A boundary: a process, a machine, a project, what is outside."""
        dash = ' stroke-dasharray="6 4"' if dashed else ""
        self.back.append(
            f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="10" fill="var(--arch-group, #f3f5f8)"'
            f' stroke="currentColor" stroke-opacity="0.45"{dash}/>'
        )
        self.back.append(self._text(x + 12, y + 20, label, size=12.5, weight="600", opacity=0.8))

    def card(self, name, x, y, w, title, lines=(), h=None, accent=None, dashed=False):
        """A part: a title and a few lines of text."""
        h = h or self._card_height(lines)
        self.boxes[name] = (x, y, w, h)
        stroke = ACCENT[accent]
        width = 1.8 if accent else 1.1
        dash = ' stroke-dasharray="5 4"' if dashed else ""
        self.front.append(
            f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="6" fill="var(--arch-card, #ffffff)"'
            f' stroke="{stroke}" stroke-width="{width}"{dash}/>'
        )
        self._card_text(x, y, w, title, lines)

    def store(self, name, x, y, w, title, lines=(), h=None, dashed=False):
        """Something kept on disk: a cylinder."""
        h = h or self._card_height(lines) + 10
        self.boxes[name] = (x, y, w, h)
        e = 7  # the height of the ellipse's half
        dash = ' stroke-dasharray="5 4"' if dashed else ""
        attrs = f'fill="var(--arch-card, #ffffff)" stroke="currentColor" stroke-width="1.1"{dash}'
        self.front.append(
            f'<path d="M{x},{y + e} a{w / 2},{e} 0 0,0 {w},0 a{w / 2},{e} 0 0,0 {-w},0 v{h - 2 * e}'
            f' a{w / 2},{e} 0 0,0 {w},0 v{-(h - 2 * e)}" {attrs}/>'
        )
        self._card_text(x, y + 10, w, title, lines)

    def person(self, name, x, y, w, title, lines=()):
        """Someone who uses gmd: a head and shoulders above the name."""
        h = 52 + 20 + LINE_HEIGHT * len(lines)
        self.boxes[name] = (x, y, w, h)
        cx = x + w / 2
        attrs = 'fill="var(--arch-card, #ffffff)" stroke="currentColor" stroke-width="1.3"'
        self.front.append(f'<circle cx="{cx}" cy="{y + 14}" r="12" {attrs}/>')
        self.front.append(
            f'<path d="M{cx - 24},{y + 52} q0,-24 24,-24 q24,0 24,24 z" {attrs}/>'
        )
        self.front.append(self._text(cx, y + 70, title, size=TITLE_SIZE, weight="600", anchor="middle"))
        for i, line in enumerate(lines):
            self.front.append(self._text(cx, y + 88 + i * LINE_HEIGHT, line, size=LINE_SIZE, anchor="middle", opacity=0.8))

    def note(self, x, y, text, anchor="start", size=LABEL_SIZE, italic=True):
        for i, line in enumerate(text.split("\n")):
            self.front.append(self._text(x, y + i * 15, line, size=size, anchor=anchor, opacity=0.75, italic=italic))

    def legend(self, x, y, items=("main", "side", "dashed")):
        texts = {
            "main": "the main path",
            "side": "background work, events, optional",
            "dashed": "optional, or outside gmd",
        }
        for item in items:
            color = ACCENT["main"] if item == "main" else ACCENT["side"] if item == "side" else "currentColor"
            dash = ' stroke-dasharray="5 4"' if item == "dashed" else ""
            self.front.append(
                f'<line x1="{x}" y1="{y}" x2="{x + 34}" y2="{y}" stroke="{color}" stroke-width="2"{dash}/>'
            )
            self.front.append(self._text(x + 42, y + 4, texts[item], size=LABEL_SIZE, opacity=0.8))
            x += 60 + len(texts[item]) * 6.3

    # --- arrows ----------------------------------------------------------------------------------

    def anchor(self, name, side, f=0.5):
        """The point on a side of an element: 'l', 'r', 't' or 'b', at a fraction f along it."""
        x, y, w, h = self.boxes[name]
        return {
            "l": (x, y + h * f),
            "r": (x + w, y + h * f),
            "t": (x + w * f, y),
            "b": (x + w * f, y + h),
        }[side]

    def arrow(self, src, dst, label="", accent=None, frm="r", to="l", fs=0.5, ft=0.5, via=(),
              at=None, dashed=False, straight=False, label_side=None):
        """An arrow from one element to another, through the corners in 'via' (each an (x, y), or a
        bare number that keeps the last point's y when it follows a vertical leg, and its x after a
        horizontal one), with a label at 'at' or on the middle of its longest leg. 'straight' aligns
        the end with the start, for an arrow straight down or across."""
        start = self.anchor(src, frm, fs)
        end = self.anchor(dst, to, ft)
        if straight:
            end = (start[0], end[1]) if to in "tb" else (end[0], start[1])
        points = [start]
        for p in via:
            points.append(p)
        points.append(end)
        color = ACCENT[accent]
        dash = ' stroke-dasharray="5 4"' if dashed else ""
        marker = {None: "plain", "main": "main", "side": "side"}[accent]
        d = "M" + " L".join(f"{px:g},{py:g}" for px, py in points)
        self.middle.append(
            f'<path d="{d}" fill="none" stroke="{color}" stroke-width="1.6"{dash}'
            f' marker-end="url(#{self.name}-arrow-{marker})"/>'
        )
        if label:
            if at is None:
                legs = list(zip(points, points[1:]))
                (ax, ay), (bx, by) = max(legs, key=lambda l: abs(l[1][0] - l[0][0]) + abs(l[1][1] - l[0][1]))
                at = ((ax + bx) / 2, (ay + by) / 2)
                if label_side == "above":
                    at = (at[0], at[1] - 10)
                elif label_side == "below":
                    at = (at[0], at[1] + 12)
            self.label(at[0], at[1], label)

    def label(self, x, y, text):
        """A label on an arrow, on a patch of the background so the line under it does not cross it."""
        lines = text.split("\n")
        w = max(self._width(l, LABEL_SIZE) for l in lines) + 8
        h = 15 * len(lines) + 3
        top = y - h / 2
        self.front.append(
            f'<rect x="{x - w / 2:g}" y="{top:g}" width="{w:g}" height="{h:g}" rx="3"'
            f' fill="var(--arch-bg, #ffffff)" fill-opacity="0.92"/>'
        )
        for i, line in enumerate(lines):
            self.front.append(self._text(x, top + 13 + i * 15, line, size=LABEL_SIZE, anchor="middle"))

    # --- output ----------------------------------------------------------------------------------

    def svg(self):
        markers = "".join(
            f'<marker id="{self.name}-arrow-{key}" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7"'
            f' markerHeight="7" orient="auto-start-reverse"><path d="M0,0 L10,5 L0,10 z" fill="{ACCENT[a]}"/></marker>'
            for key, a in (("plain", None), ("main", "main"), ("side", "side"))
        )
        return "\n".join(
            [
                f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {self.width} {self.height}"'
                f' width="{self.width}" height="{self.height}" role="img" aria-labelledby="{self.name}-title"'
                f' font-family="{FONT}" style="color: var(--arch-fg, #24292f)">',
                f'<title id="{self.name}-title">{escape(self.title)}</title>',
                f"<defs>{markers}</defs>",
                f'<rect width="{self.width}" height="{self.height}" fill="var(--arch-bg, #ffffff)"/>',
                *self.back,
                *self.middle,
                *self.front,
                "</svg>",
                "",
            ]
        )

    def save(self):
        path = os.path.join(OUT, f"{self.name}.svg")
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(self.svg())
        return path

    # --- text ------------------------------------------------------------------------------------

    @staticmethod
    def _card_height(lines):
        return 30 + LINE_HEIGHT * len(lines) + (6 if lines else 0)

    def _card_text(self, x, y, w, title, lines):
        self.front.append(self._text(x + 10, y + 20, title, size=TITLE_SIZE, weight="600"))
        for i, line in enumerate(lines):
            self.front.append(self._text(x + 10, y + 39 + i * LINE_HEIGHT, line, size=LINE_SIZE, opacity=0.8))
        widest = max([self._width(title, TITLE_SIZE, bold=True)] + [self._width(l, LINE_SIZE) for l in lines])
        if widest + 20 > w:
            print(f"warning: {self.name}: text of '{title}' is about {widest + 20:.0f} wide, its card {w}", file=sys.stderr)

    @staticmethod
    def _width(text, size, bold=False):
        """About how wide a text is drawn: the average sans-serif letter is half its size and a bit."""
        return len(text) * size * (0.54 if bold else 0.47)

    @staticmethod
    def _text(x, y, text, size, weight="normal", anchor="start", opacity=1.0, italic=False):
        attrs = f'x="{x:g}" y="{y:g}" font-size="{size}" fill="currentColor"'
        if weight != "normal":
            attrs += f' font-weight="{weight}"'
        if anchor != "start":
            attrs += f' text-anchor="{anchor}"'
        if opacity != 1.0:
            attrs += f' fill-opacity="{opacity}"'
        if italic:
            attrs += ' font-style="italic"'
        return f"<text {attrs}>{escape(text)}</text>"


# ------------------------------------------------------------------------------------------------
# The diagrams, one block each
# ------------------------------------------------------------------------------------------------


def context_view(c):
    d = Diagram("1-context", "Context view: who uses gmd, and what it talks to", 960, 560)

    d.group(170, 40, 540, 470, "The developer's machine (or a server over ssh, or a container)")
    d.group(740, 40, 200, 470, "Outside", dashed=True)

    d.person("dev", 5, 175, 110, "Developer", ["in a terminal"])

    d.card("clipboard", 195, 80, 215, "Clipboard tool", ["pbcopy, wl-copy, xclip, clip.exe;", "else the terminal (OSC 52)"], dashed=True)
    d.card("browser", 490, 80, 200, "Browser", ["$BROWSER or the OS opener;", "else the link is copied"], dashed=True)
    d.card("gmd", 200, 190, 190, "gmd", ["one executable, a full", "screen terminal UI"], h=84, accent="main")
    d.card("git", 500, 190, 180, "git", ["a process per command,", "and the ssh it runs"], h=84)
    d.store("home", 195, 350, 175, "Home folder", ["~/.gmdconfig, ~/gmd.log,", "the code coloring library"])
    d.store("repo", 500, 350, 195, "The repository", [".git; gmd's .gmdconfig and", "gmd-metadata.json; its refs", "refs/gmd/, refs/gmdtags/"])

    d.card("origin", 760, 190, 160, "origin", ["the one remote gmd", "works with: GitHub,", "GitLab, any git host"])
    d.card("releases", 760, 350, 160, "GitHub releases", ["api.github.com, and", "the release binaries"])

    d.arrow("dev", "gmd", "keys, mouse", accent="main", fs=0.3, ft=0.3, straight=True, label_side="above")
    d.arrow("gmd", "dev", "the screen", frm="l", to="r", fs=0.75, ft=0.75, straight=True, label_side="below")
    d.arrow("gmd", "git", "commands", accent="main", fs=0.3, ft=0.3, straight=True, label_side="above")
    d.arrow("git", "gmd", "askpass", accent="side", frm="l", to="r", fs=0.75, ft=0.75, straight=True, label_side="below")
    d.arrow("git", "repo", "reads, writes", accent="main", frm="b", to="t", straight=True)
    d.arrow("git", "origin", "fetch, push,\npull", accent="main", fs=0.5, ft=0.4, straight=True)
    d.arrow("gmd", "home", "settings, log", frm="b", to="t", fs=0.4, straight=True)
    d.arrow("gmd", "clipboard", "copy", frm="t", to="b", fs=0.35, straight=True, dashed=True)
    d.arrow("gmd", "browser", "open a page", frm="t", to="b", fs=0.85, ft=0.2, via=[(361.5, 165), (530, 165)], dashed=True)
    d.arrow("gmd", "releases", "update check, hourly", accent="side", frm="b", to="b", fs=0.93, ft=0.5,
            via=[(376.7, 478), (840, 478)], at=(610, 478))
    d.legend(190, 538)
    return d


def code_structure_view(c):
    m = c["modules"]
    d = Diagram("2-code-structure", "Code structure view: five layers, and the calls go down", 960, 680)

    d.group(20, 40, 690, 610, "gmd: one .NET assembly, a namespace per folder")
    d.group(740, 40, 200, 610, "Outside gmd", dashed=True)

    d.card("program", 50, 75, 380, "Program.cs", ["the entry point; Autofac registers every type"], accent="main")
    d.card("cui", 50, 170, 380, f"Cui  (about {about(m['Cui'][0])} lines)", ["Terminal.Gui views, dialogs, menus, the graph:", "RepoView, Diff, Blame, Conflict, Start, WebLog, ..."], accent="main")
    d.card("server", 50, 300, 380, f"Server  (about {about(m['Server'][0])})", ["IServer, and the immutable Repo the UI draws;", "FileMonitor, the shown subset of branches"], accent="main")
    d.card("aug", 50, 425, 380, f"Server/Private/Augmented  (about {about(m['Augmented'][0])})", ["what git does not record: each commit's branch,", "the branch hierarchy; the writes that need it"], accent="main")
    d.card("git", 50, 550, 380, f"Git  (about {about(m['Git'][0])})", ["a service per git area behind IGit; a general", "git library, which GitNamespaceTest guards"], accent="main")

    d.card("common", 470, 300, 210, f"Common  (about {about(m['Common'][0])})", ["config, repo config, the", "command line, spelling"])
    d.card("install", 470, 425, 210, f"Installation  (about {about(m['Installation'][0])})", ["the self-updater"])
    d.card("utils", 470, 550, 210, f"Utils  (about {about(m['Utils'][0])})", ["Result, Log, Cmd, askpass,", "clipboard, browser, DI"])

    d.card("tgui", 760, 170, 160, "Terminal.Gui 2.5", ["the console UI toolkit,", "named only by Cui", "and Program.cs"], dashed=True)
    d.card("pkgs", 760, 300, 160, "Other packages", ["Autofac, DiffPlex,", "WeCantSpell.Hunspell,", "TextMateSharp"], dashed=True)
    d.card("gitexe", 760, 550, 160, "git executable", ["on the PATH; one", "process per command"], dashed=True)

    d.arrow("program", "cui", "IMainView", accent="main", frm="b", to="t", fs=0.3, straight=True)
    d.arrow("cui", "server", "IServer", accent="main", frm="b", to="t", fs=0.3, straight=True)
    d.arrow("server", "cui", "events, posted\nthrough IMainThread", accent="side", frm="t", to="b", fs=0.72, straight=True)
    d.arrow("server", "aug", "IAugmentedService,\nwrite services", accent="main", frm="b", to="t", fs=0.3, straight=True)
    d.arrow("aug", "git", "IGit", accent="main", frm="b", to="t", fs=0.3, straight=True)
    d.arrow("server", "git", "IGit", frm="r", to="t", fs=0.75, ft=0.93, via=[(450, 351), (450, 528), (403.4, 528)], at=(450, 460))
    d.arrow("cui", "git", "IGit, a few\ndirect uses", frm="l", to="l", fs=0.5, ft=0.5, via=[(32, 213), (32, 594)], dashed=True, at=(60, 400))
    d.arrow("git", "utils", "ICmd", accent="main", straight=True, fs=0.5)
    d.arrow("utils", "gitexe", "starts", accent="main", straight=True, fs=0.5)
    d.arrow("cui", "tgui", "draws with", straight=True, fs=0.4, ft=0.3)
    d.arrow("install", "cui", "UI.Post,\nUI.InfoMessage", accent="side", frm="r", to="r", fs=0.5, ft=0.85, via=[(695, 451), (695, 227.8)], dashed=True, at=(600, 228))
    d.note(480, 645 - 10, "Utils is used by every layer; the arrows to it are left out")
    d.legend(40, 668)
    return d


def runtime_view(c):
    d = Diagram("3-runtime", "Runtime view: from a key or a changed file to the redrawn log", 1110, 720)
    X = 150  # the developer's column, left of the three groups

    d.group(X + 20, 40, 290, 640, "Cui, on the UI main loop")
    d.group(X + 340, 40, 300, 640, "Server and Augmented, on the thread pool")
    d.group(X + 670, 40, 270, 640, "git, a process per command")

    d.person("user", 25, 300, 120, "Developer", ["presses keys,", "reads the log"])

    d.card("input", X + 40, 80, 250, "RepoViewInput", ["a key or a click, e.g. c, p, m"], accent="main")
    d.card("cmds", X + 40, 190, 250, "*Commands, CommandRunner", ["the command, with progress", "shown and an error box"], accent="main")
    d.card("refresh", X + 40, 320, 250, "RepoView refresh", ["ShowRefreshedRepoAsync, with", "the shown branches' names"], accent="main")
    d.card("draw", X + 40, 450, 250, "ContentView, RepoWriter", ["draws only the rows on", "screen, on demand"], accent="main")
    d.card("viewrepo", X + 40, 570, 250, "ViewRepo, GraphCreater", ["a snapshot of the shown", "repo, and its graph"], accent="main")

    d.card("paused", X + 360, 80, 250, "Server, in Paused", ["the write, with the file", "monitor paused meanwhile"], accent="main")
    d.card("monitor", X + 360, 190, 250, "FileMonitor", ["watches the working folder", "and .git, debounced 1 s"], accent="side")
    d.card("aug", X + 360, 320, 250, "AugmentedService", ["the git reads, in parallel,", "into a GitRepo"], accent="main")
    d.card("augmenter", X + 360, 450, 250, "Augmenter, in Task.Run", ["infers each commit's branch", "into an immutable Repo"], accent="main")
    d.card("viewcreater", X + 360, 570, 250, "ViewRepoCreater", ["the commits of the", "branches shown"], accent="main")

    d.card("write", X + 690, 80, 230, "git commit, push, ...", ["the write, through IGit,", "the Git services and Cmd"], accent="main")
    d.card("other", X + 690, 190, 230, "Editor, another git", ["anything that changes", "the files or the refs"], dashed=True)
    d.card("reads", X + 690, 320, 230, "git log, status, ...", ["log, branches, tags, status,", "stashes, worktrees, reflog"], accent="main")

    ux = d.anchor("user", "t")[0]
    d.arrow("user", "input", "1 a key", accent="main", frm="t", to="l", via=[(ux, d.anchor("input", "l")[1])],
            at=(ux, 200))
    d.arrow("input", "cmds", "2 command", accent="main", frm="b", to="t", straight=True)
    d.arrow("cmds", "paused", "3 write", accent="main", fs=0.5, ft=0.5, via=[(X + 325, 224), (X + 325, 114)],
            at=(X + 325, 170))
    d.arrow("paused", "write", "4 git", accent="main", straight=True, label_side="above")
    d.arrow("paused", "monitor", "pauses", accent="side", frm="b", to="t", fs=0.5, straight=True, dashed=True)
    d.arrow("cmds", "refresh", "5 Refresh()", accent="main", frm="b", to="t", straight=True)
    d.arrow("other", "monitor", "files change", accent="side", frm="l", to="r", straight=True, label_side="above")
    d.arrow("monitor", "refresh", "5 RepoChanged,\nposted to the UI", accent="side", frm="l", to="r", fs=0.75, ft=0.25,
            via=[(X + 335, 241), (X + 335, 337)], at=(X + 325, 289))
    d.arrow("refresh", "aug", "6 read", accent="main", fs=0.72, ft=0.72, straight=True, label_side="below")
    d.arrow("aug", "reads", "7 git", accent="main", straight=True, label_side="above")
    d.arrow("aug", "augmenter", "8 GitRepo", accent="main", frm="b", to="t", straight=True)
    d.arrow("augmenter", "viewcreater", "9 Repo", accent="main", frm="b", to="t", straight=True)
    d.arrow("viewcreater", "viewrepo", "10 view Repo", accent="main", frm="l", to="r", straight=True, label_side="above")
    d.arrow("viewrepo", "draw", "11 rows", accent="main", frm="t", to="b", straight=True)
    d.arrow("draw", "user", "12 the redrawn\nlog on screen", accent="main", frm="l", to="b",
            via=[(ux, d.anchor("draw", "l")[1])], at=(ux, 437))
    d.legend(X + 40, 704)
    return d


def inference_view(c):
    d = Diagram("4-branch-inference", "Branch inference view: which branch each commit is on", 960, 600)

    d.group(20, 40, 220, 500, "Read from git and .git")
    d.group(270, 40, 470, 500, "Augmented: the inference, a pipeline of stages")
    d.group(770, 40, 170, 500, "Server and Cui")

    d.card("log", 40, 80, 180, "git log", [f"the newest {c['max_commits']:,} commits,", "with their subjects"])
    d.card("branches", 40, 190, 180, "Branches", ["local and remote tips"])
    d.card("reflog", 40, 290, 180, "Reflogs", ["HEAD's, each branch's", "and each remote's"])
    d.store("meta", 40, 400, 180, "gmd-metadata.json", ["branches set by hand,", "what the reflog said"])

    d.card("augmenter", 290, 80, 170, "Augmenter", ["the git records into a", "WorkRepo: branches", "paired with their", "remotes, tags, stashes"], h=130)
    d.card("names", 290, 290, 170, "BranchNameService", ["branch names parsed", "from merge subjects"])
    d.card("witness", 290, 400, 170, "ReflogWitness", ["where commits were", "made, branches started"])

    d.card("graph", 520, 80, 210, "1. CommitGraphService", ["branch tips, parents and", "children; swaps the parents", "of pull and foxtrot merges"], accent="main")
    d.card("assign", 520, 220, 210, "2. CommitBranchService", ["a branch for every commit:", f"CommitBranchRules, {c['rules']} rules", "in order of evidence, the", "first that applies wins, else", "ambiguous (BranchAmbiguity)"], accent="main")
    d.card("hierarchy", 520, 410, 210, "3. BranchHierarchyService", ["each branch's parent,", "the root branch, ancestors"], accent="main")

    d.card("repo", 785, 80, 140, "Repo", ["every commit's", "branch, every", "branch's parent"], accent="main")
    d.card("view", 785, 220, 140, "ViewRepoCreater", ["the branches", "the user shows"], accent="main")
    d.person("user", 795, 330, 120, "Developer", ["Set Branch, when", "a commit is", "ambiguous"])

    for src, f in (("log", 0.15), ("branches", 0.38), ("reflog", 0.6), ("meta", 0.82)):
        y0 = d.anchor(src, "r")[1]
        y1 = d.anchor("augmenter", "l", f)[1]
        d.arrow(src, "augmenter", "", accent="main", ft=f, via=[(255, y0), (255, y1)])
    d.arrow("augmenter", "graph", "WorkRepo", accent="main", fs=0.3, ft=0.5, straight=True, label_side="above")
    d.arrow("graph", "assign", "", accent="main", frm="b", to="t", straight=True)
    d.arrow("assign", "hierarchy", "", accent="main", frm="b", to="t", straight=True)
    d.arrow("names", "assign", "names", frm="r", to="l", fs=0.15, straight=True, label_side="above")
    d.arrow("witness", "assign", "witnessed", frm="r", to="l", fs=0.5, ft=0.931, via=[(490, 434), (490, 328)], at=(490, 385))
    d.arrow("hierarchy", "repo", "", accent="main", frm="r", to="l", fs=0.5, ft=0.5, via=[(755, 438), (755, 124)])
    d.arrow("repo", "view", "", accent="main", frm="b", to="t", straight=True)
    d.arrow("user", "meta", "set by hand", accent="side", frm="b", to="b", fs=0.5, ft=0.5,
            via=[(855, 560), (130, 560)], at=(500, 560))
    d.legend(40, 588)
    return d


def login_view(c):
    d = Diagram("5-login", "Login view: git's questions asked in gmd's own dialog", 960, 500)

    d.group(20, 40, 360, 400, "gmd on screen")
    d.group(410, 40, 530, 400, "Processes git starts")

    d.card("cmd", 40, 80, 320, "A command, e.g. Push", ["Cmd starts git with GIT_TERMINAL_PROMPT=0,", "SSH_ASKPASS=gmd, the channel and a token"], accent="main")
    d.card("server", 40, 210, 320, "AskpassServer", ["a named pipe for this user only;", "checks the token, one question at a time"], accent="side")
    d.card("dialog", 40, 330, 320, "LoginDlg", ["the passphrase, password or", "host key question, in a dialog"], accent="main")

    d.card("git", 430, 80, 200, "git", ["stdin closed, no", "terminal to ask on"], accent="main")
    d.card("ssh", 700, 80, 220, "ssh", ["SSH_ASKPASS_REQUIRE=force", "(OpenSSH 8.4 and later)"], accent="main")
    d.card("askpass", 700, 210, 220, "gmd as the askpass", ["GMD_ASKPASS=1: Main calls", "Askpass.Answer at once"], accent="side")
    d.card("never", 430, 330, 490, "No channel: a background fetch (Askpass.NeverAsk)", ["the askpass answers nothing and says why on stderr,", "where LoginError finds it; gmd says a login is needed"], dashed=True)

    d.arrow("cmd", "git", "1 git push", accent="main", fs=0.35, ft=0.35, straight=True, label_side="above")
    d.arrow("git", "ssh", "2 ssh", accent="main", fs=0.35, ft=0.35, straight=True, label_side="above")
    d.arrow("ssh", "askpass", "3 the question", accent="side", frm="b", to="t", fs=0.3, straight=True)
    d.arrow("askpass", "ssh", "6 the answer\non stdout", accent="side", frm="t", to="b", fs=0.72, straight=True)
    d.arrow("askpass", "server", "4 question + token", accent="side", frm="l", to="r", fs=0.35, ft=0.35, straight=True, label_side="above")
    d.arrow("server", "askpass", "5 answer, or Cancel", accent="side", frm="r", to="l", fs=0.75, ft=0.75, straight=True, label_side="below")
    d.arrow("server", "dialog", "asks", accent="side", frm="b", to="t", fs=0.5, straight=True)
    d.arrow("askpass", "never", "", frm="b", to="t", fs=0.5, ft=0.836, straight=True, dashed=True)
    d.legend(40, 470)
    return d


def test_view(c):
    unit_methods, unit_integration, _ = c["unit"]
    e2e_methods, _, _ = c["e2e"]
    d = Diagram("6-tests", "Test view: from a fake git to the real binary in a terminal", 960, 470)

    d.group(20, 40, 230, 210, f"gmdE2eTest: about {about(e2e_methods)} tests")
    d.group(280, 40, 660, 210, f"gmdTest: about {about(unit_methods)} tests, run one at a time")

    d.card("tmux", 40, 80, 190, "TmuxSession", ["the built binary in tmux,", "8 tests at a time; the", "screen captured as text", "and as colors"])
    d.card("builder", 300, 80, 200, "RepoBuilder, GraphText", ["a GitRepo by hand, the", "real inference, the graph", "as a text picture"])
    d.card("fakecmd", 520, 80, 190, "FakeCmd", ["canned git output for", "the Git services, which", "parse it; no process"])
    d.card("temprepo", 730, 80, 190, "TempRepo", ["real git, a temp folder;", f"about {about(unit_integration)} Integration", "tests, real repos too"])

    d.group(20, 290, 920, 130, "gmd")
    d.card("lcui", 40, 330, 190, "Cui", ["views, graph, keys"])
    d.card("laug", 300, 330, 200, "Server, Augmented", ["the inference"])
    d.card("lgit", 520, 330, 190, "Git", ["services, parsers"])
    d.card("lcmd", 730, 330, 90, "Cmd", ["processes"])
    d.card("lexe", 840, 330, 80, "git", ["the CLI"], dashed=True)

    d.arrow("tmux", "lcui", "keys in, screen out", accent="side", frm="b", to="t", straight=True)
    d.arrow("builder", "laug", "", accent="main", frm="b", to="t", straight=True)
    d.arrow("builder", "lcui", "", accent="main", frm="b", to="t", fs=0.12, ft=0.8, via=[(324, 280), (192, 280)])
    d.arrow("fakecmd", "lgit", "", accent="main", frm="b", to="t", fs=0.4, straight=True)
    d.arrow("temprepo", "lgit", "", accent="main", frm="b", to="t", fs=0.1, ft=0.85, via=[(749, 300), (681.5, 300)])
    d.arrow("temprepo", "lcmd", "", accent="main", frm="b", to="t", fs=0.5, ft=0.5, via=[(825, 270), (775, 270)])
    d.arrow("lcui", "laug", "", straight=True)
    d.arrow("laug", "lgit", "", straight=True)
    d.arrow("lgit", "lcmd", "", straight=True)
    d.arrow("lcmd", "lexe", "", straight=True)
    d.note(40, 446, "Every push runs both projects in CI (build.yml); the end-to-end tier is about five minutes on four cores.", italic=True)
    d.legend(40, 462, items=("main", "side"))
    return d


def delivery_view(c):
    d = Diagram("7-delivery", "Delivery view: from a commit to a user's machine", 960, 640)

    d.group(20, 40, 640, 560, "GitHub: michael-reichenauer/gmd")
    d.group(690, 40, 250, 560, "Users", dashed=True)

    d.card("feature", 40, 80, 250, "Feature branch", ["mr/<topic> or a thread's branch,", "a pull request into dev"], accent="main")
    d.card("build", 370, 80, 270, "build.yml, every push and PR", ["format check, both test projects,", "a linux-only publish; no release"])
    d.card("dev", 40, 210, 250, "dev", ["merges only; CHANGELOG.md's", "Unreleased notes written here"], accent="main")
    d.card("release", 370, 200, 270, "release.yml, run by hand", ["a preview builds dev as it is; a", "release stamps the notes, merges", "dev into main with --no-ff, raises", "dev's minor; pushes after tests"], accent="main")
    d.card("main", 40, 360, 250, "main", ["releases only, moved by", "the Release workflow"], accent="main")
    d.card("ghrelease", 370, 360, 270, "GitHub Release v<a.b.c.d>", ["4 platform binaries, gmdSetup.exe,", "2 copies under old names, SHA256SUMS"], accent="main")
    d.card("pages", 370, 480, 270, "branch-graph.yml", ["gmd --web-log on every push, hourly", "and after a release: GitHub Pages"], accent="side")
    d.card("feature-wf", 40, 480, 250, "devcontainer-feature.yml", ["tests the feature on a change,", "publishes it to ghcr.io from main"], accent="side")

    d.card("updater", 710, 80, 210, "gmd's own updater", ["hourly check; checksum, runs", "the new binary, then swaps it"], accent="main")
    d.card("scripts", 710, 190, 210, "install.sh, install.ps1", ["latest release, checked", "against SHA256SUMS"])
    d.card("brew", 710, 300, 210, "Homebrew tap", ["a formula per release,", "pushed by release.yml"], dashed=True)
    d.card("web", 710, 410, 210, "The web branch graph", ["michael-reichenauer.github.io"], dashed=True)
    d.card("devcontainer", 710, 500, 210, "Dev container feature", ["ghcr.io/.../gmd/gmd:1, the", "latest release per build"], dashed=True)

    d.arrow("feature", "build", "push", straight=True, fs=0.5, ft=0.5, label_side="above")
    d.arrow("feature", "dev", "merged", accent="main", frm="b", to="t", straight=True)
    d.arrow("dev", "release", "run by hand", accent="main", fs=0.4, ft=0.35, straight=True, label_side="above")
    d.arrow("release", "main", "--no-ff merge", accent="main", frm="l", to="t", fs=0.85, ft=0.6, via=[(190, 293.5)], at=(260, 293.5))
    d.arrow("release", "ghrelease", "publish", accent="main", frm="b", to="t", fs=0.5, straight=True)
    for target in ("updater", "scripts", "brew"):
        y = d.anchor(target, "l")[1]
        d.arrow("ghrelease", target, "", accent="main", fs=0.5, via=[(675, 394), (675, y)])
    d.arrow("main", "feature-wf", "", accent="side", frm="b", to="t", straight=True)
    d.arrow("feature-wf", "devcontainer", "", accent="side", frm="b", to="l", fs=0.5, ft=0.5, via=[(165, 580), (695, 580), (695, 534)], dashed=True)
    d.arrow("pages", "web", "", accent="side", frm="r", to="l", fs=0.2, ft=0.5, via=[(670, 493.6), (670, 436)], dashed=True)
    d.legend(40, 625)
    return d


VIEWS = [context_view, code_structure_view, runtime_view, inference_view, login_view, test_view, delivery_view]


def main():
    c = counts()
    if "--count" in sys.argv[1:]:
        print_counts(c)
        return
    for view in VIEWS:
        print(view(c).save())


if __name__ == "__main__":
    main()
