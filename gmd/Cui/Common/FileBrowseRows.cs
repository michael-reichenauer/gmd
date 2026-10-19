namespace gmd.Cui.Common;

// How the file browser draws its rows, each as wide as the list: the changed files and the tree
// while nothing is typed, and a file found once something is. All have at the right the letter of a
// file's change in the commit, in the color the commit details and the diff give it, so the files it
// changed stand out wherever they are. Plain, with no view, so the rows are asserted as text.
static class FileBrowseRows
{
    // A row of what is shown while nothing is typed: a heading, white with a dark rule after it, a
    // changed file as its path, lined up with the names of the tree, or a row of the tree
    public static Text TreeRow(FileTree.Row row, int width) =>
        row.Kind switch
        {
            FileTree.RowKind.Heading => Text.Black(" ")
                .White(row.Name)
                .Dark($" {new string('─', Math.Max(0, width - row.Name.Length - 3))}")
                .Black(" "),
            FileTree.RowKind.Changed => PathRow(row.Path, row.Change, [], width, "   "),
            _ => TreeNodeRow(row, width),
        };

    // A dark guide for each folder the row is in, under that folder's marker, then a folder's '▾'
    // when open or '▸' when closed and its name in the light blue of the key hints (cyan), or a
    // file's name, lined up with the names of the folders beside it. A folder holding a changed file
    // has a '•' where a file has its letter.
    static Text TreeNodeRow(FileTree.Row row, int width)
    {
        var text = new TextBuilder().Black(" ");
        for (var i = 0; i < row.Depth; i++)
            text.Dark("│ ");
        if (row.IsFolder)
            text.Dark(row.IsOpen ? "▾ " : "▸ ");
        else
            text.Black("  ");

        var mark =
            row.IsFolder && row.HasChanges ? Text.Yellow("•")
            : !row.IsFolder && row.Change != ' ' ? ChangedFileText.Letter(row.Change)
            : Text.Black(" ");
        var nameWidth = width - text.Length - 3;
        var name = CutEnd(row.Name, nameWidth);
        if (row.IsFolder)
            text.Cyan(name);
        else
            text.White(name);

        return WithMark(text, mark, width);
    }

    // A file found: its folders dark and its name white, the words typed in yellow where they are,
    // and a path too long for the row cut at its start, where its folders are
    public static Text FoundRow(string path, char change, IReadOnlyList<string> words, int width) =>
        PathRow(path, change, words, width, " ");

    static Text PathRow(string path, char change, IReadOnlyList<string> words, int width, string margin) =>
        PathRow(
            path,
            Color.White,
            change != ' ' ? ChangedFileText.Letter(change) : Text.Black(" "),
            words,
            width,
            margin
        );

    // A path, its folders dark and its name in the color given, the words in yellow, and the mark at
    // the right; the folder browser's too, with its own separator
    internal static Text PathRow(
        string path,
        Color nameColor,
        Text mark,
        IReadOnlyList<string> words,
        int width,
        string margin
    )
    {
        var shown = CutStart(path, width - margin.Length - mark.Length - 2);
        // A separator at the end is the root's, '/', which is a name rather than a folder it is in
        var slash = shown.Length > 1 ? shown[..^1].LastIndexOfAny(['/', Path.DirectorySeparatorChar]) : -1;
        Text text = new TextBuilder().Black(margin).Dark(shown[..(slash + 1)]).Color(nameColor, shown[(slash + 1)..]);
        foreach (var word in words)
            text = text.WithMatches(word, margin.Length, Color.BrightYellow);

        return WithMark(text.ToTextBuilder(), mark, width);
    }

    // The mark one column in from the right edge, the row padded to it
    internal static Text WithMark(TextBuilder text, Text mark, int width)
    {
        var padding = Math.Max(1, width - text.Length - mark.Length - 1);
        return text.Black(new string(' ', padding)).Add(mark).Black(" ");
    }

    internal static string CutEnd(string text, int width) =>
        text.Length <= width || width < 2 ? text : text[..(width - 1)] + "…";

    static string CutStart(string text, int width) =>
        text.Length <= width || width < 2 ? text : "…" + text[(text.Length - (width - 1))..];
}
