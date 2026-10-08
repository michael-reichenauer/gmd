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
            .Item("Commit ...", "c", () => cmds.CommitFromMenu(false), () => !isStatusOK, () => "Nothing to commit")
            // Enabled as the 'a' key is: a commit not yet pushed can be amended with no changes too,
            // to reword its message
            .Item(
                "Amend ...",
                "a",
                () => cmds.CommitFromMenu(true),
                () => CommitRewrite.IsNotPushed(repo.Repo, cc),
                () => "Only a commit not yet pushed can be amended"
            )
            .Items(GetAmendOlderItems(c, cc))
            // Straight in the menu rather than in a 'Rebase' sub menu of its own, which held nothing
            // else and named a rebase that it is not
            .Items(GetSquashItems())
            .Item("Commit Diff", "d", () => cmds.ShowCurrentRowDiff())
            .SubMenu("Undo", "", GetCommitUndoItems())
            .SubMenu("Stash", "", GetStashMenuItems())
            // On any row, since its list of every tag is not about the commit
            .SubMenu("Tag", "", GetTagItems())
            .Item(
                "Create Branch from Commit ...",
                "b",
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
            .Item(
                "Copy Commit Id",
                "i",
                () => repo.Cmds.CopyCommitId(),
                () => !c.IsUncommitted,
                () => RepoCommands.WhyNoCopy
            )
            .Item(
                "Copy Commit Message",
                "⇧i",
                () => repo.Cmds.CopyCommitMessage(),
                () => !c.IsUncommitted,
                () => RepoCommands.WhyNoCopy
            )
            // Above the file items rather than at the end, which the walks to Blame File count from
            .Item(
                "Open Commit in Browser",
                "",
                () => repo.Cmds.OpenCommitInBrowser(c.Id),
                () => WebCommands.CanOpenCommit(repo.Repo, c),
                () => WebCommands.WhyNoOpenCommit(repo.Repo, c)
            )
            // Belongs with the commit items: the files offered are the ones this commit has, even
            // though the history then shown for the chosen one is its full history, hence 'Full'
            .Item("Full File History ...", "", () => cmds.ShowFileHistory())
            // The same file list, answering the other question: not how the file changed over
            // time, but which commit each line of it as it stands came from
            .Item("Blame File ...", "", () => cmds.BlameFile())
            .Separator()
            // Everything about branches, including showing and hiding them, is under here
            .SubMenu("Branches", "", branchMenu.GetShownBranchesItems())
            .SubMenu("Repo Menu", "⇧m", repoMenu.GetRepoMenuItems());
    }

    IEnumerable<MenuItem> GetCommitUndoItems()
    {
        string id = repo.RowCommit.Id;
        var binaryPaths = repo
            .Repo.Status.AddedFiles.Concat(repo.Repo.Status.ModifiedFiles)
            .Concat(repo.Repo.Status.RenamedTargetFiles)
            .Where(f => !Files.IsText(Path.Join(repo.Path, f)))
            .ToList();

        // The last change of the current branch, which the reflog says, and its undo, which a
        // second Undo takes back
        var current = repo.Repo.CurrentBranch();
        var step = BranchUndo.StepOf(repo.Repo, current);
        var whyNotDrop = CommitRewrite.WhyNotDrop(repo.Repo, repo.RowCommit);

        return Menu
            .Items.Item(
                BranchUndo.Label(step),
                "",
                () => repo.BranchCmds.UndoLastChange(current.Name),
                () => BranchUndo.WhyNot(repo.Repo, current, step) == "",
                () => BranchUndo.WhyNot(repo.Repo, current, step)
            )
            // The work no branch has any more, which the reflogs still know of
            .Item("Recover Lost Commits ...", "", () => repo.BranchCmds.RecoverLostCommits())
            // The branches gmd deleted, whose reflogs git deleted with them
            .Item("Restore Deleted Branch ...", "", () => repo.BranchCmds.RestoreDeletedBranch())
            .Separator()
            .SubMenu(
                "Discard Changes in a File",
                "",
                GetUncommittedFileItems(),
                () => cmds.CanUndoUncommitted(),
                () => "There are no uncommitted changes to undo"
            )
            .Item("Revert Commit", "", () => cmds.UndoCommit(id), () => repo.Repo.Status.IsOk, () => Why.Changes)
            // Revert's twin for a commit not pushed yet: out of the branch rather than undone by
            // another commit
            .Item(
                repo.RowCommit.IsUncommitted ? "Drop Commit" : $"Drop {id.Sid()}",
                "",
                () => cmds.DropCommit(id),
                () => whyNotDrop == "",
                () => whyNotDrop
            )
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
            )
            // The same and the ignored files too, i.e. the folder as a fresh clone would have it.
            // Enabled with no changes as well, since deleting what git ignores is reason enough.
            .Item("Discard All Changes and Ignored Files", "", () => repo.Cmds.CleanWorkingFolder());
    }

    // The amend of the commit the menu is for, when that is an older one than the last, whose own
    // amend is the item above it, with the 'a' key. The key amends the last commit wherever the
    // cursor is, so that only an item naming the commit rewrites an older one.
    IEnumerable<MenuItem> GetAmendOlderItems(Commit c, Commit cc)
    {
        if (c.IsUncommitted || c.Id == cc.Id)
            return [];

        var whyNot = CommitRewrite.WhyNotAmend(repo.Repo, c);
        return Menu.Items.Item(
            $"Amend {Sid(c.Id)} ...",
            "",
            () => cmds.AmendOlderCommit(c.Id),
            () => whyNot == "",
            () => whyNot
        );
    }

    // Squashes the commits selected with Shift-↑↓, which the item names once there are some
    IEnumerable<MenuItem> GetSquashItems()
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
            () => selected == "" ? "Select the commits to squash with ⇧↑↓ first" : Why.Changes
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
            // Pop, but the stash is kept, e.g. for the same changes on another branch as well
            .SubMenu(
                "Stash Apply",
                "",
                GetStashApplyItems(),
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
                "t",
                () => cmds.AddTag(),
                () => !repo.RowCommit.IsUncommitted,
                () => "A tag is put on a commit: move to one first"
            )
            .SubMenu("Remove Tag", "", GetDeleteTagItems(), whyNot: () => "The commit has no tags")
            .Item("Tags ...", "", () => cmds.ShowTags());

    IEnumerable<MenuItem> GetStashPopItems() =>
        repo.Repo.Stashes.Select(s => Menu.Item($"{s.Message}", "", () => cmds.StashPop(s.Name)));

    IEnumerable<MenuItem> GetStashApplyItems() =>
        repo.Repo.Stashes.Select(s => Menu.Item($"{s.Message}", "", () => cmds.StashApply(s.Name)));

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
