using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.Diff;

// The list beside a wide diff: its files, or the commits of a file history, each the row of the diff
// it goes to, which the list follows as the diff is moved through. The rows and the index math, with
// no view, so it is unit testable.
class DiffFileList
{
    // A file or a commit: what the list draws of it, the diff row it starts at, and a file's path
    public record Item(Text Text, int Row, string Path);

    // The list is drawn on a terminal at least this wide, where the diff beside it is still side by
    // side, and only for more than one file, which it would add nothing to
    public const int MinTerminalWidth = 150;
    const int MinWidth = 24;
    const int MaxWidth = 40;

    public IReadOnlyList<Item> Items { get; }
    public bool IsCommits { get; }

    DiffFileList(IReadOnlyList<Item> items, bool isCommits)
    {
        Items = items;
        IsCommits = isCommits;
    }

    public static readonly DiffFileList None = new([], false);

    // Each item found by its id or path in a dictionary, the first of them as a search would: a search
    // for each made it quadratic, 78 ms for a commit of 5000 files, with every v, l, h, + or w
    public static DiffFileList Of(CommitDiff[] diffs, DiffRows rows)
    {
        if (diffs.Length > 1)
        {
            var byId = FirstBy(diffs, d => d.Id);
            var commits = rows
                .Rows.Select((r, i) => (r, i))
                .Where(x => x.r.CommitId != "")
                .Select(x => (Diff: byId.GetValueOrDefault(x.r.CommitId), Row: x.i))
                .Where(x => x.Diff != null)
                .Select(x => new Item(Text.Cyan(x.Diff!.Id.Sid()).White($" {x.Diff.Message}"), x.Row, ""))
                .ToList();
            return new DiffFileList(commits, true);
        }

        var byPath = FirstBy(diffs.Length == 1 ? diffs[0].FileDiffs : [], f => f.PathAfter);
        var items = rows
            .Rows.Select((r, i) => (r, i))
            .Where(x => x.r.FilePath != "")
            .Select(x => (File: byPath.GetValueOrDefault(x.r.FilePath), Row: x.i))
            .Where(x => x.File != null)
            .Select(x => new Item(FileText(x.File!), x.Row, x.File!.PathAfter))
            .ToList();
        return new DiffFileList(items, false);
    }

    // How wide the list is drawn on a terminal of the width, its separator included, 0 when it is not
    public int WidthFor(int terminalWidth)
    {
        if (Items.Count < 2 || terminalWidth < MinTerminalWidth)
            return 0;
        // The mark, the separator after a space, and the view's scrollbar column, which the rows are
        // drawn without: uncounted, the widest file was always cut
        var widest = Items.Max(i => i.Text.Length) + 4;
        return Math.Clamp(widest, MinWidth, Math.Min(MaxWidth, terminalWidth / 4));
    }

    static Dictionary<string, T> FirstBy<T>(IEnumerable<T> items, Func<T, string> keyOf)
    {
        Dictionary<string, T> first = [];
        foreach (var item in items)
            first.TryAdd(keyOf(item), item);
        return first;
    }

    // The item the diff row is in, the last one starting at or above it, -1 above them all
    public int IndexAt(int row)
    {
        var index = -1;
        for (var i = 0; i < Items.Count && Items[i].Row <= row; i++)
            index = i;
        return index;
    }

    // The rows of the list, of the width with the separator: the title, then an item per row, the
    // one the diff's cursor is in marked, and highlighted while the list has the keys. leftOut is how
    // many lines of a file are left out of the commit, said with the '╎' the diff marks them with.
    public IReadOnlyList<Text> Rows(int width, int current, bool isFocused, Func<string, int> leftOut)
    {
        var textWidth = Math.Max(0, width - 2);
        var title = Text.White(IsCommits ? "Commits " : "Files ").Dark($"{Items.Count}");
        List<Text> rows = [Separated(title, textWidth)];
        for (var i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            var count = item.Path != "" ? leftOut(item.Path) : 0;
            var mark = i == current ? Text.Magenta("▌") : Text.Black(" ");
            var note = count > 0 ? $" ╎{count}" : "";
            var text = mark.Add(Cut(item.Text, textWidth - 1 - note.Length)).Yellow(note);
            var row = Separated(text, textWidth);
            rows.Add(i == current && isFocused ? Highlighted(row, textWidth) : row);
        }
        return rows;
    }

    // A file as the list names it, with its letter and its lines: its path's folder dark, so that its
    // name stands out
    static Text FileText(FileDiff fd)
    {
        var path = DiffRowService.PathOf(fd);
        var slash = path.LastIndexOf('/');
        var text = ChangedFileText.Letter(DiffRowService.KindOf(fd)).Black(" ");
        if (slash > 0 && !fd.IsRenamed)
            text.Dark(path[..(slash + 1)]);
        text.White(slash > 0 && !fd.IsRenamed ? path[(slash + 1)..] : path);
        if (fd.IsBinary)
            text.Dark("  bin");
        else
            ChangedFileText.AddCounts(
                text,
                DiffRowService.CountOf(fd, DiffMode.DiffAdded),
                DiffRowService.CountOf(fd, DiffMode.DiffRemoved)
            );
        return text;
    }

    // Cut to the width, keeping the end, where a file's name and its lines are, behind a '…'
    static Text Cut(Text text, int width) =>
        text.Length <= width || width < 2
            ? text
            : text.Subtext(0, 2).ToTextBuilder().Dark("…").Add(text.Subtext(text.Length - (width - 3), width - 3));

    static Text Separated(Text text, int width) => text.Subtext(0, width, isFillRest: true).ToTextBuilder().Dark(" │");

    static Text Highlighted(Text row, int width) =>
        row.Subtext(0, width).ToHighlight().ToTextBuilder().Add(row.Subtext(width, row.Length - width));
}
