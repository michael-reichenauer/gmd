namespace gmd.Cui.Common;

// How the folder browser draws its rows, each as wide as the list, as the file browser draws its own
// (FileBrowseRows): a folder of the tree, and a folder found for what is typed. A repository says so
// at the right, where the file browser has the letter of a file's change. Plain, with no view, so the
// rows are asserted as text.
static class FolderBrowseRows
{
    const string RepoMark = "repo";

    // A dark guide for each folder the row is in, under that folder's marker, then '▾' when the
    // folder is open, '▸' when it can be opened and nothing when it has no folders in it, and its
    // name in the light blue of the file browser's folders
    public static Text TreeRow(FolderTree.Row row, int width)
    {
        var text = new TextBuilder().Black(" ");
        for (var i = 0; i < row.Depth; i++)
            text.Dark("│ ");
        text.Dark(
            row.IsOpen ? "▾ "
            : row.IsEmpty ? "  "
            : "▸ "
        );

        // A root is named by its whole path, e.g. the start folder and the folder it is in, which is
        // cut at its start, as a folder found is, since its last folder is what tells two apart
        var mark = Mark(row.IsRepo);
        var nameWidth = width - text.Length - mark.Length - 2;
        text.Cyan(
            row.Depth == 0 && row.Name != FolderTree.Name(row.Path)
                ? FileBrowseRows.CutStart(row.Name, nameWidth)
                : FileBrowseRows.CutEnd(row.Name, nameWidth)
        );
        return FileBrowseRows.WithMark(text, mark, width);
    }

    // Where a click opens or closes the folder of a row: its marker, after the guides
    public static bool IsOnMarker(FolderTree.Row row, int x) => x >= 1 + 2 * row.Depth && x < 3 + 2 * row.Depth;

    // A folder found, by its path as shown: the folders it is in dark and its name light blue, the
    // words typed in yellow wherever they are, and a path too long for the row cut at its start
    public static Text FoundRow(string shownPath, bool isRepo, IReadOnlyList<string> words, int width) =>
        FileBrowseRows.PathRow(shownPath, Color.Cyan, Mark(isRepo), words, width, " ");

    static Text Mark(bool isRepo) => isRepo ? Text.Dark(RepoMark) : Text.Black(new string(' ', RepoMark.Length));
}
