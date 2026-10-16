using gmd.Cui.Common;

namespace gmd.Cui.Commits;

// The files a commit takes, each ticked or not: the checklist of the commit dialog. Every file is
// ticked at first, which is what a commit has always taken, and unticking one leaves it as it is,
// uncommitted. It replaces a staging area gmd never had, and is the guard against committing a
// stray .env or a debug log along with the rest (.notes/USABILITY.md, the product review). For the amend
// of an older commit none is ticked at first, since that is most often a new message, and work in
// progress should not end up in an old commit unasked.
//
// The decision and the paths, with no view, so it is tested without a terminal; CommitDlg draws it.
class CommitFiles
{
    // A file as the checklist shows it: what changed ('M' modified, 'A' new, 'D' deleted, 'R'
    // renamed), the path, and the paths git commits for it, which for a rename are both its old and
    // its new path, since the one goes and the other comes
    public record File(char Kind, string Path, IReadOnlyList<string> GitPaths);

    readonly bool[] ticked;
    readonly LeftOutLines? leftOut;

    // leftOut is the lines left out of the commit, chosen in the diff, which a ticked file with some
    // is committed without (LeftOutLines)
    public CommitFiles(Server.Status status, bool isTicked = true, LeftOutLines? leftOut = null)
    {
        this.leftOut = leftOut;
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
        ticked = Enumerable.Repeat(isTicked, Files.Count).ToArray();
    }

    public IReadOnlyList<File> Files { get; }

    public int TickedCount => ticked.Count(t => t);

    public bool IsTicked(int index) => ticked[index];

    // How many lines of the file are left out, read when asked, since they can be changed in the diff
    // while the dialog is open (Ctrl-D)
    public int LeftOutCount(int index) => leftOut == null ? 0 : Files[index].GitPaths.Sum(leftOut.CountOf);

    // The ticked files with lines left out, which only a commit of the uncommitted changes leaves out
    public IReadOnlyList<File> TickedWithLinesLeftOut =>
        Files.Where((_, i) => ticked[i] && LeftOutCount(i) > 0).ToList();

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
    public IReadOnlyList<string>? PathsToCommit => TickedCount == ticked.Length ? null : TickedPaths;

    // The paths of the ticked files, none when none is
    public IReadOnlyList<string> TickedPaths => Files.Where((_, i) => ticked[i]).SelectMany(f => f.GitPaths).ToList();
}
