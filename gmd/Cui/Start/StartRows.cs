using gmd.Cui.Common;

namespace gmd.Cui.Start;

// The version shown in the title, and the newer one there is to update to, "" when there is none
record StartInfo(string Version, string NewVersion = "");

enum StartAction
{
    OpenRepo,
    OpenFolder,
    Clone,
    Init,
    Update,
    Help,
    About,
    Quit,
}

// What a row of the start page opens or does: a listed repository, which has a number for the key
// that opens it (0 from the tenth on, which have none), or one of the actions, which have a letter
record StartItem(StartAction Action, StartRepo? Repo = null, int Number = 0)
{
    public string Key =>
        Action switch
        {
            StartAction.OpenRepo => Number > 0 ? $"{Number}" : "",
            StartAction.OpenFolder => "o",
            StartAction.Clone => "c",
            StartAction.Init => "i",
            StartAction.Update => "u",
            StartAction.Help => "?",
            StartAction.About => "a",
            StartAction.Quit => "q",
            _ => "",
        };

    // The same item on a page laid out again, e.g. after a repository was removed from the list,
    // which renumbers the ones after it
    public bool IsSame(StartItem other) => other.Action == Action && other.Repo?.Path == Repo?.Path;
}

// The start page laid out for one size: its rows, and the item on each, which is what the keys
// that move between items, a click and the highlight of the selected item need. One item per row.
class StartPage
{
    readonly IReadOnlyList<Text> rows;
    readonly IReadOnlyList<int> rowItems; // The index of the item on each row, or -1
    readonly int barStart; // Where the highlight of the selected item starts and ends, the same for all
    readonly int barEnd;

    public StartPage(IReadOnlyList<Text> rows, IReadOnlyList<int> rowItems, IReadOnlyList<StartItem> items)
    {
        this.rows = rows;
        this.rowItems = rowItems;
        Items = items;

        var itemRows = rows.Where((_, i) => rowItems[i] >= 0).ToList();
        barStart = itemRows.Select(r => r.ToString().TakeWhile(c => c == ' ').Count()).DefaultIfEmpty(2).Min() - 2;
        barEnd = itemRows.Select(r => r.Length).DefaultIfEmpty(0).Max() + 1;
    }

    public IReadOnlyList<StartItem> Items { get; }
    public int Count => rows.Count;

    // The item selected when the page is shown: the most recent repository, so Enter opens it, or
    // with none, the first action
    public int FirstItem
    {
        get
        {
            var repo = IndexOf(i => i.Action == StartAction.OpenRepo);
            return repo >= 0 ? repo : IndexOf(i => i.Action == StartAction.OpenFolder);
        }
    }

    public int RowOf(int item)
    {
        for (int i = 0; i < rowItems.Count; i++)
        {
            if (rowItems[i] == item)
                return i;
        }
        return -1;
    }

    public int ItemAt(int row) => row >= 0 && row < rowItems.Count ? rowItems[row] : -1;

    public int Step(int item, int step) => Math.Clamp(item + step, 0, Items.Count - 1);

    public int IndexOf(Func<StartItem, bool> predicate)
    {
        for (int i = 0; i < Items.Count; i++)
        {
            if (predicate(Items[i]))
                return i;
        }
        return -1;
    }

    // A row as drawn, the selected item marked and highlighted. The highlight is as long for every
    // item, so moving between them moves a bar rather than a ragged edge.
    public Text Row(int row, bool isSelected)
    {
        var text = rows[row];
        if (!isSelected || ItemAt(row) < 0)
            return text;

        var bar = Text.White("▸").Add(text.Subtext(barStart + 1, barEnd - barStart - 1, true)).ToText().ToHighlight();
        return Text.Add(text.Subtext(0, barStart, true))
            .Add(bar)
            .Add(text.Subtext(barEnd, Math.Max(0, text.Length - barEnd)))
            .ToText();
    }
}

// Lays out the start page, the view gmd shows when it is started outside a repository: what it is,
// why this page is shown, the repositories to open, each with the number key that opens it, and
// the actions, each with its letter key.
//
//      Gmd 1.4 · a Git client where you choose which branches are shown
//      ~/Downloads is not a git repository
//
//      Recent repositories
//    ▸ 1  gmd          ~/code/gmd
//      2  web-shop     ~/code/web-shop
//
//      Repositories in ~/Downloads
//      3  scratch      ~/Downloads/scratch
//
//      o  Open a folder ...
//      c  Clone a repository ...
//      ...
//
// Someone who has opened no repository with gmd yet is told what each action does, beside it. The
// page is centered when the terminal is wider than it, and on one too short for it, parts are left
// out, least needed first, so the actions are never what is scrolled out of sight. A layout, with no
// view, so it is tested without a terminal; StartView draws it.
static class StartRows
{
    const int indent = 4; // Where the text starts. The marker of the selected item is 2 before it.
    const int labelX = 7; // Where an item's label starts, after its number or key
    const int minPathWidth = 12; // Less room than this and the path is left out
    const int minExplainWidth = 12;
    const int shortListCount = 3;
    const string tagline = "a Git client where you choose which branches are shown";

    // How much of the page is kept
    record Fit(bool IsSpaced, bool IsTitle, bool IsWhy, int MaxRecent, int MaxInFolder);

    public static StartPage Of(StartRepos repos, StartInfo info, int width, int height)
    {
        StartPage? page = null;
        foreach (var fit in Fits())
        {
            page = Layout(repos, info, width, fit);
            if (page.Count <= height)
                break;
        }

        return page!;
    }

    // All of the page, then less of it: first the spacing and the title, then the line saying why
    // the page is shown, then the repositories beyond the first few, the ones in the folder first.
    // Whatever is still too tall scrolls.
    static IEnumerable<Fit> Fits()
    {
        var all = new Fit(true, true, true, int.MaxValue, int.MaxValue);
        var dense = all with { IsSpaced = false, IsTitle = false };
        yield return all;
        yield return dense;
        yield return dense with
        {
            IsWhy = false,
        };
        yield return dense with
        {
            IsWhy = false,
            MaxInFolder = shortListCount,
        };
        yield return dense with
        {
            IsWhy = false,
            MaxInFolder = shortListCount,
            MaxRecent = shortListCount,
        };
    }

    static StartPage Layout(StartRepos repos, StartInfo info, int width, Fit fit)
    {
        List<Text> rows = [];
        List<int> rowItems = [];
        List<StartItem> items = [];

        void Line(Text text)
        {
            rows.Add(text);
            rowItems.Add(-1);
        }
        void Blank()
        {
            if (fit.IsSpaced)
                Line(Text.Empty);
        }
        void Item(StartItem item, Text text)
        {
            rows.Add(text);
            rowItems.Add(items.Count);
            items.Add(item);
        }

        Blank();
        if (fit.IsTitle)
            Line(Indented(Text.BrightMagenta("Gmd").Dark($" {info.Version} · {tagline}"), width));
        if (fit.IsWhy && !repos.IsFolderInRepo)
            Line(Indented(Text.White(CutStart(repos.ShownFolder, width / 2)).Dark(" is not a git repository"), width));
        if (info.NewVersion != "")
        { // Never left out, since a new version is said nowhere else on the page but the bar's ⇓
            Blank();
            var update = Text.BrightGreen($"Update to Gmd {info.NewVersion} ...");
            Item(new StartItem(StartAction.Update), ItemRow("u", update, width));
        }

        // The repositories, numbered across both lists, with the names lined up across both
        var recent = repos.Recent.Take(fit.MaxRecent).ToList();
        var inFolder = repos.InFolder.Take(fit.MaxInFolder).ToList();
        var nameWidth = Math.Clamp(
            recent.Concat(inFolder).Select(r => r.Name.Length).DefaultIfEmpty(0).Max(),
            8,
            width >= 100 ? 24 : 16
        );
        var number = 0;
        void Repos(string header, IReadOnlyList<StartRepo> shown, int more)
        {
            if (!shown.Any())
                return;

            Blank();
            Line(Indented(Text.BrightMagenta(header), width));
            foreach (var repo in shown)
            {
                number++;
                var item = new StartItem(StartAction.OpenRepo, repo, number <= 9 ? number : 0);
                Item(item, RepoRow(item, nameWidth, width));
            }
            if (more > 0)
                Line(Indented(Text.Dark($"… and {more} more"), width));
        }
        Repos("Recent repositories", recent, repos.Recent.Count - recent.Count);
        Repos(
            $"Repositories in {CutStart(repos.ShownFolder, width / 2)}",
            inFolder,
            repos.InFolder.Count - inFolder.Count + repos.MoreInFolder
        );

        // The actions, said what they do to someone who has not opened a repository with gmd before
        Blank();
        var isExplained = !repos.Recent.Any();
        var actions = Actions(repos, isExplained, width);
        var explainX = labelX + actions.Max(a => a.Label.Length) + 3;
        var explainWidth = width - explainX - 1;
        foreach (var (action, label, explanation) in actions)
        {
            var text = Text.White(label);
            if (isExplained && explanation != "" && explainWidth >= minExplainWidth)
                text.Black(new string(' ', explainX - labelX - label.Length)).Dark(CutEnd(explanation, explainWidth));
            Item(new StartItem(action), ItemRow(new StartItem(action).Key, text, width));
        }

        return new StartPage(Centered(rows, width), rowItems, items);
    }

    static IReadOnlyList<(StartAction Action, string Label, string Explanation)> Actions(
        StartRepos repos,
        bool isExplained,
        int width
    )
    {
        // Init where gmd was started, said by name while that is short enough to read at a glance. Said
        // beside it instead when the actions are explained, which keeps the explanations' column in.
        var folder = repos.ShownFolder;
        var isNamed = !isExplained && folder.Length <= 30 && labelX + 25 + folder.Length < width;
        var (init, initExplained) =
            repos.IsFolderInRepo || repos.Folder == "" ? ("Init a repository ...", "start a new, empty one")
            : isNamed ? ($"Init a repository in {folder} ...", "")
            : ("Init a repository here ...", $"start a new one in {folder}");

        return
        [
            (StartAction.OpenFolder, "Open a folder ...", "browse to a repository on this computer"),
            (StartAction.Clone, "Clone a repository ...", "copy one from GitHub or another server"),
            (StartAction.Init, init, initExplained),
            (StartAction.Help, "Help", "how to read the log, in the browser"),
            (StartAction.About, "About", "the version, and where to report a problem"),
            (StartAction.Quit, "Quit", ""),
        ];
    }

    //     1  gmd          ~/code/gmd
    static Text RepoRow(StartItem item, int nameWidth, int width)
    {
        var repo = item.Repo!;
        var name = repo.Name.Length > nameWidth ? repo.Name[..(nameWidth - 1)] + "…" : repo.Name;
        var text = Text.White(name);

        var pathX = labelX + nameWidth + 2;
        var pathWidth = width - pathX - 1;
        if (pathWidth >= minPathWidth)
            text.Black(new string(' ', pathX - labelX - name.Length)).Dark(CutStart(repo.ShownPath, pathWidth));

        return ItemRow(item.Key, text, width);
    }

    //     o  Open a folder ...
    static Text ItemRow(string key, Text label, int width)
    {
        var row = Text.Black(new string(' ', indent)).Cyan(key).Black(new string(' ', labelX - indent - key.Length));
        return row.Add(label).ToText().Subtext(0, Math.Max(0, width - 1));
    }

    static Text Indented(Text text, int width) =>
        Text.Black(new string(' ', indent)).Add(text).ToText().Subtext(0, Math.Max(0, width - 1));

    // Shifted right to the middle when there is room to spare, all rows alike so they stay lined up
    static IReadOnlyList<Text> Centered(IReadOnlyList<Text> rows, int width)
    {
        var used = rows.Select(r => r.Length).DefaultIfEmpty(0).Max() + indent;
        var pad = Math.Max(0, (width - used) / 2);
        if (pad == 0)
            return rows;

        var margin = new string(' ', pad);
        return rows.Select(r => r.Length == 0 ? r : Text.Black(margin).Add(r).ToText()).ToList();
    }

    // A path cut from the start, since its end is what tells it apart: '…/code/gmd'
    static string CutStart(string text, int width) =>
        text.Length <= width ? text
        : width <= 1 ? "…"
        : "…" + text[(text.Length - width + 1)..];

    static string CutEnd(string text, int width) =>
        text.Length <= width ? text
        : width <= 1 ? "…"
        : text[..(width - 1)] + "…";
}
