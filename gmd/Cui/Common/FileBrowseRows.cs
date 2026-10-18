namespace gmd.Cui.Common;

// How the file browser draws its rows, each as wide as the list: a row of the tree while nothing is
// typed, and a file found once something is. Both have at the right the letter of a file's change in
// the commit, in the color the commit details and the diff give it, so the files it changed stand
// out wherever they are. Plain, with no view, so the rows are asserted as text.
static class FileBrowseRows
{
    // A dark guide for each folder the row is in, under that folder's marker, then a folder's '▾'
    // when open or '▸' when closed and its name in blue, or a file's name, lined up with the names
    // of the folders beside it. A folder holding a changed file has a '•' where a file has its letter.
    public static Text TreeRow(FileTree.Row row, int width)
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
            text.BrightBlue(name);
        else
            text.White(name);

        return WithMark(text, mark, width);
    }

    // A file found: its folders dark and its name white, the words typed in yellow where they are,
    // and a path too long for the row cut at its start, where its folders are
    public static Text FoundRow(string path, char change, IReadOnlyList<string> words, int width)
    {
        var mark = change != ' ' ? ChangedFileText.Letter(change) : Text.Black(" ");
        var shown = CutStart(path, width - 4);
        var slash = shown.LastIndexOf('/');
        Text text = new TextBuilder().Black(" ").Dark(shown[..(slash + 1)]).White(shown[(slash + 1)..]);
        foreach (var word in words)
            text = text.WithMatches(word, 1, Color.BrightYellow);

        return WithMark(text.ToTextBuilder(), mark, width);
    }

    // The mark one column in from the right edge, the row padded to it
    static Text WithMark(TextBuilder text, Text mark, int width)
    {
        var padding = Math.Max(1, width - text.Length - 2);
        return text.Black(new string(' ', padding)).Add(mark).Black(" ");
    }

    static string CutEnd(string text, int width) =>
        text.Length <= width || width < 2 ? text : text[..(width - 1)] + "…";

    static string CutStart(string text, int width) =>
        text.Length <= width || width < 2 ? text : "…" + text[(text.Length - (width - 1))..];
}
