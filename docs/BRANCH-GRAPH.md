# The branch graph on the web

gmd's branch graph, published as a web page you can read on a phone or a tablet:
**https://michael-reichenauer.github.io/gmd/**

GitHub's own views of branches, the network graph under Insights and the branch list, are hard
to read: they show you that branches exist, but not how they relate or what is going on in them.
gmd shows that well, but gmd is a terminal app and needs a computer. This page brings gmd's graph
to any browser, for one purpose: to see at a glance what everyone, people and Claude alike, is
working on across the branches, and to jump from there to the commits and pull requests on GitHub.

It is a view only. You cannot show or hide branches, diff or commit there; for that, use gmd itself
or GitHub.

## Reading the page

The buttons beside the title open the repository on GitHub, its pull requests and its Actions, the
runs of its workflows, and, for a repository on github.com, its files in
[VS Code for the Web](https://vscode.dev), an editor in the browser with nothing to install.

**Branches** lists the active branches drawn in the graph, those whose latest commit is among the
commits shown, the one with the latest commit first, with who made that commit and how long ago. A
branch's dot has its color in the graph. Tap a name to jump to its latest commit in the graph. A green `#n` is the branch's open pull request (grey while it is a
draft; one from a fork is left out); `↗` opens the branch on GitHub when it has no pull request.

**Commits** is gmd's log of all those branches, newest first, as `Show All Active` shows it in gmd:

- Each branch is a vertical line in its own color, main in magenta on the left, and every branch
  to the right of the branch it came from. A dot is a commit on that branch.
- An arc into a line is a merge: the branch on the right was merged into the line it joins. An arc
  out of a line is where a branch started.
- Where a branch is merged in and another branches out at the same commit, or lines otherwise
  share a stretch, each keeps its own color and the lines run side by side, the one going up
  above the one coming from below. The terminal has one character for such a place, so it draws
  it white there.
- The short grey arcs are branches gmd does not show, for example ones that were merged and
  deleted long ago: where they were merged in or branched out.
- A short line above a branch's latest commit says the branch is open for more commits.
- A colored outlined label is a branch's tip, its latest commit, and a green label beside it its
  open pull request. A label with green text on grey is a tag, such as a release version, and opens
  its release on GitHub.
- A commit's subject is beside its dot, with the author, the time and the id on the line below,
  followed by the start of the rest of the commit's message, as far as the width allows (150
  characters at most). For a merge that is what it merged, which its subject seldom says.
- The subject opens the commit's diff on GitHub, or, for a "Merge pull request #n" commit, that
  pull request. The short id at the end of the second line opens the commit too.
- Times are shown as how long ago; older than a month, as the date.

A button or a link to GitHub opens in another tab, so the graph stays open in its tab, and the
next one is shown in that same tab rather than in one more: tap ten commits, a pull request and
Actions, and there is one other tab, showing Actions. On an iPad or an iPhone, where Safari shows
a link in that tab but leaves it behind the graph, the page closes the tab and opens the link in a
new one, so that it comes to the front there too.

The page is dark, in the colors of gmd's dark palette, whatever the device's mode, as gmd is best known. It shows the latest 400
commits.

Which commit belongs to which branch is not something git records. gmd infers it from the shape
of the history and from merge messages, as it does in the terminal, so the page shows the same
branches gmd would, including those already merged whose names only survive in a merge message.

## How it is made

Nothing runs on a server: the page is one static HTML file, rebuilt and published by a GitHub
Action.

An open page keeps itself current: as it is opened, every five minutes, and each time it is shown
again, on a switch back to its tab, its window or the browser, it reads itself anew and, when a
newer page is published, shows that page's branches and commits in place, without a reload, so it
stays where it was scrolled to. This only reads the published file; it starts no workflow run.

1. **The workflow**, `.github/workflows/branch-graph.yml` ("Branch graph"), runs on every push
   to any branch, whenever a pull request is opened, closed or changes draft state, when the
   Release workflow ends (whose pushes to `dev` and `main`, made with the workflow's own token,
   start no run themselves), every hour, and by hand from the Actions tab (`Run workflow`).
   GitHub runs the hourly and the after-release runs from the default branch's workflow file only,
   so those two start once the workflow is on `main`.
2. It checks out the repository with its whole history and every branch, and builds gmd.
3. It reads the open pull requests with `gh pr list` into a JSON file.
4. It runs gmd, which writes the page and exits, with no terminal and no UI:

   ```bash
   gmd --web-log site/index.html --prs prs.json --max 400 --hide stats
   ```

   gmd reads the repository exactly as it does when you open it, shows all active branches, hides
   the `stats` branch (the download snapshots, which are no one's work), and writes the page.
5. The page is uploaded and deployed to GitHub Pages. Each run replaces the page; a run started
   while another is going cancels the older one.

A run takes about a minute, mostly building gmd, so the page is a minute or two behind a push.

### Inside gmd

The command is in `gmd/Common/ProgramCommands.cs`, and the page is made in `gmd/Cui/WebLog/`:

- `WebLogExport` reads the repo through the server, as the log view does, shows all active branches,
  hides the branches `--hide` names, leaves out each local branch that is at its remote branch's
  commit (the terminal draws it as a tip of its own beside the remote branch's, which on the page
  only takes a column), and builds gmd's graph of it with `GraphCreater`.
- `WebLogPage` writes the HTML: the branch list, a row per commit with its labels and links, the
  colors of gmd's dark palette as CSS, and a few lines of script that turn the times into
  "how long ago". It makes the GitHub links with `Git/WebLinks.cs`, which also knows GitLab,
  Bitbucket, Azure DevOps and Gitea, so the page works for repositories hosted there too.
- `GraphSvg` draws each row of the graph. It takes the very runes the terminal shows, `┣ ┃ ╭ ╯ ─`,
  from `GraphWriter`, and draws each as the lines it is made of: heavy for a branch, light for a
  merge, with the corners as arcs and the commit as a dot. Where lines of several colors share a
  rune, which the terminal draws white, it draws them from the graph's own parts instead (each
  `GraphColumn` keeps every line with its color), each in its color and side by side. Text cannot be used as it is, since a
  browser does not join one row of box-drawing characters to the next the way a terminal does.

The page is the terminal's graph under a new coat, so any improvement to gmd's graph shows on the
page as well.

### Running it yourself

For any repository, with no workflow:

```bash
gmd --web-log graph.html -d path/to/repo
```

| Option            | What it does                                                            |
| ----------------- | ----------------------------------------------------------------------- |
| `--web-log <file>` | Writes the page to `<file>`                                            |
| `-d <folder>`     | The repository; the current folder when left out                        |
| `--prs <file>`    | The open pull requests, as `gh pr list --json number,title,headRefName,url,isDraft,isCrossRepository` writes them |
| `--max <n>`       | At most `<n>` commits (500 when left out)                               |
| `--hide <names>`  | Branches to leave out, comma separated, named as on the remote          |

gmd writes `~/.gmdconfig` and `~/gmd.log` as it always does, so the workflow gives it a throwaway
`HOME`.

## Setting it up for a repository

The workflow needs two settings, once:

1. **Settings → Pages → Source: GitHub Actions.**
2. **Settings → Environments → github-pages → Deployment branches and tags: No restriction**, since
   a push to any branch updates the page. By default only the default branch may deploy.

A public repository's page is public, like the repository. A private repository needs a plan that
offers Pages for private repositories, and its page is then public unless the plan can restrict it.
