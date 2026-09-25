using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.RepoView;

interface ICommitMenu
{
    void Show(int x, int y, int index);
    void ShowStashMenu(int x = Menu.Center, int y = 0);
}

class CommitMenu : ICommitMenu
{
    readonly IRepoMenu repoMenu;
    readonly IBranchMenu branchMenu;
    readonly IViewRepo repo;
    readonly ICommitCommands cmds;

    public CommitMenu(IRepoMenu repoMenu, IBranchMenu branchMenu, IViewRepo repo)
    {
        this.repoMenu = repoMenu;
        this.branchMenu = branchMenu;
        this.repo = repo;
        this.cmds = repo.CommitCmds;
    }

    public void Show(int x, int y, int index)
    {
        var c = repo.Repo.ViewCommits[index];
        Menu.Show($"Commit: {Sid(c.Id)}", x, y + 2, GetCommitMenuItems(c.Id));
    }

    public void ShowStashMenu(int x = Menu.Center, int y = 0)
    {
        Menu.Show("Stash", x, y + 2, GetStashMenuItems());
    }

    IEnumerable<MenuItem> GetCommitMenuItems(string commitId)
    {
        var c = repo.Repo.CommitById[commitId];
        var cc = repo.Repo.CurrentCommit();
        var rb = repo.RowBranch;
        var cb = repo.Repo.CurrentBranch();
        var isStatusOK = repo.Repo.Status.IsOk;
        var sid = Sid(c.Id);

        return Menu
            .Items.Items(repoMenu.GetNewReleaseItems())
            .Item("Commit ...", "C", () => cmds.CommitFromMenu(false), () => !isStatusOK, () => "Nothing to commit")
            // Enabled as the 'a' key is: a commit not yet pushed can be amended with no changes too,
            // to reword its message
            .Item(
                "Amend ...",
                "A",
                () => cmds.CommitFromMenu(true),
                () => cc.IsAhead,
                () => "Only a commit not yet pushed can be amended"
            )
            .Item("Commit Diff", "D", () => cmds.ShowCurrentRowDiff())
            .SubMenu("Undo", "", GetCommitUndoItems())
            .SubMenu("Rebase", "", GetRebaseMenuItems())
            .SubMenu("Stash", "", GetStashMenuItems())
            .SubMenu(
                "Tag",
                "",
                GetTagItems(),
                () => c.Id != Repo.UncommittedId,
                () => "A tag is put on a commit: move to one first"
            )
            .Item(
                "Create Branch from Commit ...",
                "B",
                () => repo.BranchCmds.CreateBranchFromCommit(),
                () => !c.IsUncommitted,
                () => "A branch starts at a commit: move to one first"
            )
            .Item(
                $"Merge Commit into {cb?.ShortNiceUniqueName()}",
                "",
                () => repo.BranchCmds.MergeBranch(c.Id),
                () => isStatusOK && rb != cb,
                () => !isStatusOK ? Why.Changes : "The commit is on the current branch already"
            )
            .Item(
                $"Cherry Pick into {cb?.ShortNiceUniqueName()}",
                "",
                () => cmds.CherryPick(),
                () => isStatusOK && rb != cb,
                () => !isStatusOK ? Why.Changes : "The commit is on the current branch already"
            )
            .Item(
                "Switch to Commit",
                "",
                () => repo.BranchCmds.SwitchToCommit(),
                () => isStatusOK && repo.RowCommit.Id != repo.Repo.CurrentCommit().Id,
                () => !isStatusOK ? Why.Changes : "Already on this commit"
            )
            .Item("Commit Details", "Enter", () => cmds.ToggleDetails())
            // Belongs with the commit items: the files offered are the ones this commit has, even
            // though the history then shown for the chosen one is its full history, hence 'Full'
            .Item("Full File History ...", "", () => cmds.ShowFileHistory())
            // The same file list, answering the other question: not how the file changed over
            // time, but which commit each line of it as it stands came from
            .Item("Blame File ...", "", () => cmds.BlameFile())
            .Separator()
            // Everything about branches, including showing and hiding them, is under here
            .SubMenu("Branches", "", branchMenu.GetShownBranchesItems())
            .SubMenu("Repo Menu", "Shift-M", repoMenu.GetRepoMenuItems());
    }

    IEnumerable<MenuItem> GetCommitUndoItems()
    {
        string id = repo.RowCommit.Id;
        var binaryPaths = repo
            .Repo.Status.AddedFiles.Concat(repo.Repo.Status.ModifiedFiles)
            .Concat(repo.Repo.Status.RenamedTargetFiles)
            .Where(f => !Files.IsText(Path.Join(repo.Path, f)))
            .ToList();

        return Menu
            .Items.SubMenu(
                "Discard Changes in a File",
                "",
                GetUncommittedFileItems(),
                () => cmds.CanUndoUncommitted(),
                () => "There are no uncommitted changes to undo"
            )
            .Item("Revert Commit", "", () => cmds.UndoCommit(id), () => repo.Repo.Status.IsOk, () => Why.Changes)
            .Item(
                "Uncommit Last Commit",
                "",
                () => cmds.UncommitLastCommit(),
                () => cmds.CanUncommitLastCommit(),
                () => !repo.Repo.Status.IsOk ? Why.Changes : "Only a commit not yet pushed can be uncommitted"
            )
            .Item(
                $"Uncommit {id.Sid()} and Newer",
                "",
                () => cmds.UncommitUntilCommit(id),
                () => repo.Repo.Status.IsOk && (repo.RowBranch.IsCurrent || repo.RowBranch.IsLocalCurrent),
                () => !repo.Repo.Status.IsOk ? Why.Changes : "Only commits on the current branch can be uncommitted"
            )
            .Separator()
            .Item(
                "Discard Changes in Binary Files",
                "",
                () => cmds.UndoUncommittedFiles(binaryPaths),
                () => binaryPaths.Any(),
                () => "There are no uncommitted binary files"
            )
            .Item(
                "Discard All Changes",
                "",
                () => repo.Cmds.UndoAllUncommittedChanged(),
                () => cmds.CanUndoUncommitted(),
                () => "There are no uncommitted changes to undo"
            );
    }

    IEnumerable<MenuItem> GetRebaseMenuItems()
    {
        var selection = repo.RepoView.Selection;
        var (i1, i2) = (selection.I1, selection.I2);
        var selected = "";
        Commit? c1 = null;
        Commit? c2 = null;
        if (!selection.IsEmpty && i2 - i1 > 0)
        { // User selected range of commits
            c1 = repo.Repo.ViewCommits[i1];
            c2 = repo.Repo.ViewCommits[i2];
            if (!c1.IsUncommitted && !c2.IsUncommitted)
            {
                selected = $"{Sid(c1.Id)}...{Sid(c2.Id)}";
            }
        }

        return Menu.Items.Item(
            selected == "" ? "Squash ..." : $"Squash {selected} ...",
            "",
            () => cmds.SquashCommits(c1!.Id, c2!.Id),
            () => !selection.IsEmpty && selected != "" && repo.Status.IsOk,
            () => selected == "" ? "Select the commits to squash with Shift-↑↓ first" : Why.Changes
        );
    }

    IEnumerable<MenuItem> GetStashMenuItems() =>
        Menu
            .Items.Item(
                "Stash Changes ...",
                "",
                () => cmds.Stash(),
                () => !repo.Status.IsOk,
                () => "No changes to stash"
            )
            .SubMenu(
                "Stash Pop",
                "",
                GetStashPopItems(),
                () => repo.Status.IsOk,
                () => !repo.Status.IsOk ? Why.Changes : NoStashes
            )
            .SubMenu("Stash Diff", "", GetStashDiffItems(), whyNot: () => NoStashes)
            .SubMenu("Stash Drop", "", GetStashDropItems(), whyNot: () => NoStashes);

    const string NoStashes = "There are no stashes";

    IEnumerable<MenuItem> GetTagItems() =>
        Menu
            .Items.Item(
                "Add Tag ...",
                "T",
                () => cmds.AddTag(),
                () => !repo.RowCommit.IsUncommitted,
                () => "A tag is put on a commit: move to one first"
            )
            .SubMenu("Remove Tag", "", GetDeleteTagItems(), whyNot: () => "The commit has no tags");

    IEnumerable<MenuItem> GetStashPopItems() =>
        repo.Repo.Stashes.Select(s => Menu.Item($"{s.Message}", "", () => cmds.StashPop(s.Name)));

    IEnumerable<MenuItem> GetStashDropItems() =>
        repo.Repo.Stashes.Select(s => Menu.Item($"{s.Message}", "", () => cmds.StashDrop(s.Name)));

    IEnumerable<MenuItem> GetStashDiffItems() =>
        repo.Repo.Stashes.Select(s => Menu.Item($"{s.Message}", "", () => cmds.StashDiff(s.Name)));

    IEnumerable<MenuItem> GetDeleteTagItems()
    {
        var commit = repo.RowCommit;
        return commit.Tags.Select(t => Menu.Item(t.Name, "", () => cmds.DeleteTag(t.Name)));
    }

    IEnumerable<MenuItem> GetUncommittedFileItems() =>
        repo.Repo.GetUncommittedFiles().Select(f => Menu.Item(f, "", () => cmds.UndoUncommittedFile(f)));

    public static string Sid(string id) => id == Repo.UncommittedId ? "uncommitted" : id.Sid();
}
