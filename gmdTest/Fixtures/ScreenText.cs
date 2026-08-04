using System.Text;
using System.Text.RegularExpressions;

namespace gmdTest.Fixtures;

// Normalizes a screen captured from a tmux pane, so it can be asserted as a picture of what the
// user sees. GraphText does the same job for the graph column alone; this does it for the whole
// terminal.
//
// Most of the screen is already deterministic, because the fixture pins it rather than this
// masking it afterwards: E2eRepo pins the commit dates and identity (so the time column, and the
// commit ids derived from them, are the same on every machine), TmuxSession pins TZ and the
// locale, and the pane size is fixed so the column widths and the application bar's space filler
// cannot move. That is deliberate — an assertion on a real sid is worth more than one on a
// placeholder. What genuinely cannot be pinned is the temp repository path, which is a new guid
// every run, so that is what gets replaced.
//
// Use like e.g.:
//     ScreenText.AssertEqual(
//         """
//          Gmd {repo}, ●main                                            (main) [Ϙ Search] ? X
//         ────────────────────────────────────────────────────────────────────────────────────
//         ┣ ● Initial                                          0a02af Test User  24-10-15 12:00
//         """,
//         gmd.WaitFor("Initial"),
//         repo.Path);
static class ScreenText
{
    // What a replaced repository path is written as
    public const string RepoPlaceholder = "{repo}";

    // ApplicationBar.maxRepoLength, i.e. how much of the path the application bar keeps
    const int MaxRepoLength = 30;

    // The ESC that starts every color sequence in an escaped capture
    const char Escape = '\u001b';

    // 'yy-MM-dd HH:mm', as RepoWriter.WriteTime writes it
    static readonly Regex TimeRegex = new(@"\d\d-\d\d-\d\d \d\d:\d\d");

    // The whole screen: the repository path replaced, every line right trimmed and the trailing
    // blank lines dropped
    public static string Of(string capture, string repoPath = "") => Join(Lines(capture, repoPath));

    // A window of rows, for the screens where only part of it is the subject of the test
    public static string Rows(string capture, string repoPath, int first, int count) =>
        Join(Lines(capture, repoPath).Skip(first).Take(count));

    // Replaces the time on the rows containing 'onRowsWith' with a placeholder of the same width,
    // so the column stays aligned and its position is still asserted.
    //
    // For the one row whose time cannot be pinned: the uncommitted row, whose time is DateTime.Now
    // rather than a commit date. It is row-targeted rather than whole-screen deliberately — the
    // commit rows on the same screen do have pinned times, and those are worth asserting.
    public static string MaskTimes(string screen, string onRowsWith) =>
        Join(screen.Split('\n').Select(l => l.Contains(onRowsWith) ? TimeRegex.Replace(l, "NN-NN-NN NN:NN") : l));

    // The color of every character on the screen as one letter each, lined up under the text of
    // Of(), which is what GraphText.ColorsOf does for the graph column alone. Takes an escaped
    // capture (TmuxSession.CaptureColors()), not a plain one.
    //
    // A blank keeps its space, so the letters line up below the characters they belong to. Note
    // that row 0 does not line up with Of(), since that replaces the repo path with a shorter
    // placeholder — everything below it does, which is where the alignment carries information.
    public static string ColorsOf(string escapedCapture) => Join(ColorLines(escapedCapture, isForeground: true));

    public static string ColorRows(string escapedCapture, int first, int count) =>
        Join(ColorLines(escapedCapture, isForeground: true).Skip(first).Take(count));

    // The same for the background, which is how the log view draws the row the cursor is on: a
    // run of 'D', i.e. a dark gray background, over the part of the row that is highlighted.
    public static string BackgroundRows(string escapedCapture, int first, int count) =>
        Join(ColorLines(escapedCapture, isForeground: false).Skip(first).Take(count));

    // Asserts the screen matches. On a mismatch the actual screen is printed as a block that can
    // be read, checked and pasted straight back into the test, which is how these snapshots are
    // written and updated in the first place.
    public static void AssertEqual(string expected, string capture, string repoPath = "")
    {
        var actual = Of(capture, repoPath);
        if (actual == expected)
            return;

        var rule = new string('-', 100);
        Assert.Fail(
            $"The screen is not what was expected.\n\n"
                + $"Expected:\n{rule}\n{expected}\n{rule}\n\n"
                + $"Actual:\n{rule}\n{actual}\n{rule}\n"
        );
    }

    // One letter per character cell, telling the color it was drawn in. Uppercase is the normal
    // color and lowercase its bright variant, so 'M' is the magenta of the main branch and 'm'
    // the bright magenta of the ' Gmd ' label. 'W' is white, 'D' dark, '.' black and '-' the
    // terminal default, i.e. nothing was set.
    //
    // The codes are what a terminal is sent, so this maps them back to the gmd colors of
    // Cui/Common/Color.cs: 30-37 are the normal colors, 90-97 the bright ones, and gmd's Dark and
    // White are bright black and bright white, hence 90 and 97.
    static readonly Dictionary<int, char> ColorLetters = new()
    {
        [30] = '.',
        [31] = 'R',
        [32] = 'G',
        [33] = 'Y',
        [34] = 'B',
        [35] = 'M',
        [36] = 'C',
        [39] = '-',
        [90] = 'D',
        [91] = 'r',
        [92] = 'g',
        [93] = 'y',
        [94] = 'b',
        [95] = 'm',
        [96] = 'c',
        [97] = 'W',
    };

    // Walks an escaped capture, tracking the color the terminal is currently set to, and emits
    // one letter per visible character. Everything drawn goes through here, so an unexpected
    // color shows up as '?' rather than being quietly dropped.
    static IEnumerable<string> ColorLines(string escapedCapture, bool isForeground)
    {
        var lines = new List<string>();

        foreach (var line in escapedCapture.Split('\n'))
        {
            var letters = new StringBuilder();
            var color = -1;
            var i = 0;

            while (i < line.Length)
            {
                // An SGR sequence, i.e. ESC [ <params> m, sets the color rather than drawing
                if (line[i] == Escape && i + 1 < line.Length && line[i + 1] == '[')
                {
                    var end = line.IndexOf('m', i);
                    if (end == -1)
                        break;

                    foreach (var part in line[(i + 2)..end].Split(';'))
                    {
                        // An empty or zero parameter is a reset of both colors
                        if (part == "" || part == "0")
                            color = -1;
                        else if (int.TryParse(part, out var code) && IsColorCode(code, isForeground))
                            color = ToForegroundCode(code, isForeground);
                    }

                    i = end + 1;
                    continue;
                }

                letters.Append(line[i] == ' ' ? ' ' : Letter(color));
                i++;
            }

            lines.Add(letters.ToString().TrimEnd());
        }

        while (lines.Count > 0 && lines[^1] == "")
            lines.RemoveAt(lines.Count - 1);

        return lines;
    }

    // Foreground is 30-39 and 90-97, background the same shifted by 10
    static bool IsColorCode(int code, bool isForeground)
    {
        var offset = isForeground ? 0 : 10;
        return (code >= 30 + offset && code <= 39 + offset) || (code >= 90 + offset && code <= 97 + offset);
    }

    static int ToForegroundCode(int code, bool isForeground) => isForeground ? code : code - 10;

    static char Letter(int color)
    {
        if (color == -1)
            return '-';
        return ColorLetters.TryGetValue(color, out var letter) ? letter : '?';
    }

    static IEnumerable<string> Lines(string capture, string repoPath)
    {
        var lines = Replace(capture, repoPath).Split('\n').Select(l => l.TrimEnd()).ToList();

        // A 40 row pane over a handful of commits is mostly empty, and dropping those rows is
        // what makes the expected value reviewable. Blank rows between content are kept.
        while (lines.Count > 0 && lines[^1] == "")
            lines.RemoveAt(lines.Count - 1);

        return lines;
    }

    static string Replace(string capture, string repoPath)
    {
        if (repoPath == "")
            return capture;

        // The application bar shows only the last 30 characters of the path, after a '┅'
        // (ApplicationBar.GetRepoPath). A temp repo path is longer than that, so the part that
        // says which fixture it is has been truncated away and nothing but the guid is left.
        if (repoPath.Length > MaxRepoLength)
            capture = capture.Replace($"┅{repoPath[^MaxRepoLength..]}", RepoPlaceholder);

        // The full path, as the commit details pane and the open repo menu show it
        return capture.Replace(repoPath, RepoPlaceholder);
    }

    static string Join(IEnumerable<string> lines) => string.Join("\n", lines);
}
