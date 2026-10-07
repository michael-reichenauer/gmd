namespace gmd.Cui;

// The files a commit takes, each ticked or not: the checklist of the commit dialog. Every file is
// ticked at first, which is what a commit has always taken, and unticking one leaves it as it is,
// uncommitted. It replaces a staging area gmd never had, and is the guard against committing a
// stray .env or a debug log along with the rest (USABILITY.md, the product review).
//
// The decision and the paths, with no view, so it is tested without a terminal; CommitDlg draws it.
class CommitFiles
{
    // A file as the checklist shows it: what changed ('M' modified, 'A' new, 'D' deleted, 'R'
    // renamed), the path, and the paths git commits for it, which for a rename are both its old and
    // its new path, since the one goes and the other comes
    public record File(char Kind, string Path, IReadOnlyList<string> GitPaths);

    readonly bool[] ticked;

    public CommitFiles(Server.Status status)
    {
        List<File> files =
        [
            .. status.ModifiedFiles.Select(p => new File('M', p, [p])),
            .. status.AddedFiles.Select(p => new File('A', p, [p])),
            .. status.DeletedFiles.Select(p => new File('D', p, [p])),
            .. status.RenamedSourceFiles.Zip(
                status.RenamedTargetFiles,
                (source, target) => new File('R', $"{source} → {target}", [source, target])
            ),
        ];
        Files = files.OrderBy(f => f.Path, StringComparer.Ordinal).ToList();
        ticked = Enumerable.Repeat(true, Files.Count).ToArray();
    }

    public IReadOnlyList<File> Files { get; }

    public int TickedCount => ticked.Count(t => t);

    public bool IsTicked(int index) => ticked[index];

    public void Toggle(int index)
    {
        if (index >= 0 && index < ticked.Length)
            ticked[index] = !ticked[index];
    }

    // All of them, or none when all are ticked, so the same key takes every file back out
    public void ToggleAll()
    {
        var isAll = TickedCount < ticked.Length;
        Array.Fill(ticked, isAll);
    }

    // The paths to commit, or null when every file is ticked, i.e. everything, which is committed
    // as a commit always was, with what git counts as a change and gmd may not list included
    public IReadOnlyList<string>? PathsToCommit =>
        TickedCount == ticked.Length ? null : Files.Where((_, i) => ticked[i]).SelectMany(f => f.GitPaths).ToList();
}
