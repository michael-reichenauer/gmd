using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.RepoView;

interface ICommitMenu
{
    void Show(int x, int y, int index);
    void ShowStashMenu(int x = Menu.Center, int y = 0);
    void ShowCopyMenu(int x, int y);
}

class CommitMenu : ICommitMenu
{
    readonly IRepoMenu repoMenu;
    readonly IBranchMenu branchMenu;
    readonly IViewRepo repo;
    readonly CommandContext commands;
    readonly ICommitCommands cmds;
    readonly DiffMark diffMark;

    public CommitMenu(
        IRepoMenu repoMenu,
        IBranchMenu branchMenu,
        IViewRepo repo,
        CommandContext commands,
        DiffMark diffMark
    )
    {
        this.repoMenu = repoMenu;
        this.branchMenu = branchMenu;
        this.repo = repo;
        this.commands = commands;
        this.diffMark = diffMark;
        this.cmds = commands.CommitCmds;
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

    // What Ctrl-C copies, asked rather than guessed, since a commit has several things worth copying
    public void ShowCopyMenu(int x, int y)
    {
        var commits = CopiedCommits();
        var title = commits.Count == 1 ? $"Copy: {Sid(commits[0].Id)}" : $"Copy: {commits.Count} Commits";
        Menu.Show(title, x, y + 2, GetCopyItems());
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
            .Item("Commit ...", "c", () => cmds.Commit(false), () => !isStatusOK, () => "Nothing to commit")
            // Enabled as the 'a' key is: the last commit can be amended with no changes too, to reword
            // its message, also once pushed, which 'p' then pushes with force
            .Item(
                "Amend ...",
                "a",
                () => cmds.Commit(true),
                () => CommitRewrite.WhyNotAmendLast(repo.Repo) == "",
                () => CommitRewrite.WhyNotAmendLast(repo.Repo)
            )
            .Items(GetAmendOlderItems(c, cc))
            // Straight in the menu rather than in a 'Rebase' sub menu of its own, which held nothing
            // else and named a rebase that it is not
            .Items(GetSquashItems())
            .Item("Commit Diff", "d", () => cmds.ShowCurrentRowDiff())
            // Any two commits, which a range selected with ⇧↑↓ is only for commits of one branch
            .Item("Mark for Diff", "", () => cmds.MarkForDiff(c.Id), () => !c.IsUncommitted, () => NoCommit)
            .Items(GetDiffWithMarkedItems(c))
            .SubMenu("Undo", "", GetCommitUndoItems())
            .SubMenu("Stash", "", GetStashMenuItems())
            // On any row, since its list of every tag is not about the commit
            .SubMenu("Tag", "", GetTagItems())
            .Item(
                "Create Branch from Commit ...",
                "b",
                () => commands.CreateCmds.CreateBranchFromCommit(),
                () => !c.IsUncommitted,
                () => BranchCreateCommands.NoCommitToStartAt
            )
            // This commit and the newer ones, e.g. made on main by mistake, which are not pushed yet
            .Item(
                "Move to New Branch ...",
                "",
                () => commands.CreateCmds.MoveCommitsToNewBranch(c.Id),
                () => CommitRewrite.WhyNotMoveToNewBranch(repo.Repo, c) == "",
                () => CommitRewrite.WhyNotMoveToNewBranch(repo.Repo, c)
            )
            // Greyed out with nothing it can do, e.g. on a commit of another branch, saying why
            .SubMenu("Reorder", "", GetReorderItems(c), whyNot: () => CommitRewrite.WhyNotMove(repo.Repo, c, true))
            .Item(
                $"Merge Commit into {cb?.ShortNiceUniqueName()}",
                "",
                () => commands.BranchCmds.MergeBranch(c.Id),
                () => isStatusOK && rb != cb,
                () => !isStatusOK ? Why.Changes : "The commit is on the current branch already"
            )
            .Item(GetCherryPickItem(rb, cb, isStatusOK))
            .Item(
                "Switch to Commit",
                "",
                () => commands.BranchCmds.SwitchToCommit(),
                () => isStatusOK && repo.RowCommit.Id != repo.Repo.CurrentCommit().Id,
                () => !isStatusOK ? Why.Changes : "Already on this commit"
            )
            .Item("Commit Details", "Enter", () => cmds.ToggleDetails())
            .SubMenu("Copy", "Ctrl-C", GetCopyItems(), () => WhyNotCopy() == "", WhyNotCopy)
            // Above the file items rather than at the end, which the walks to Blame File count from
            .Item(
                "Open Commit in Browser",
                "",
                () => commands.RepoCmds.OpenCommitInBrowser(c.Id),
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
        var whyNotUncommit = CommitRewrite.WhyNotUncommit(repo.Repo, repo.RowCommit);
        var whyNotReset = CommitRewrite.WhyNotResetTo(repo.Repo, repo.RowCommit);

        return Menu
            .Items.Item(
                BranchUndo.Label(step),
                "",
                () => commands.UndoCmds.UndoLastChange(current.Name),
                () => BranchUndo.WhyNot(repo.Repo, current, step) == "",
                () => BranchUndo.WhyNot(repo.Repo, current, step)
            )
            // The work no branch has any more, which the reflogs still know of
            .Item("Recover Lost Commits ...", "", () => commands.UndoCmds.RecoverLostCommits())
            // The branches gmd deleted, whose reflogs git deleted with them
            .Item("Restore Deleted Branch ...", "", () => commands.UndoCmds.RestoreDeletedBranch())
            // What the discards threw away, and the stashes dropped, which gmd keeps, as git does not
            .Item("Restore Discarded Changes ...", "", () => commands.UndoCmds.RestoreDiscarded())
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
                repo.RowCommit.IsUncommitted ? "Uncommit Commits" : $"Uncommit {id.Sid()} and Newer",
                "",
                () => cmds.UncommitUntilCommit(id),
                () => whyNotUncommit == "",
                () => whyNotUncommit
            )
            // To any commit, of the branch or another, asking whether to keep the changes
            .Item(
                repo.RowCommit.IsUncommitted ? "Reset Branch to Commit ..." : $"Reset Branch to {id.Sid()} ...",
                "",
                () => cmds.ResetBranchTo(id),
                () => whyNotReset == "",
                () => whyNotReset
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
                () => commands.RepoCmds.UndoAllUncommittedChanged(),
                () => cmds.CanUndoUncommitted(),
                () => "There are no uncommitted changes to undo"
            )
            // The same and the ignored files too, i.e. the folder as a fresh clone would have it.
            // Enabled with no changes as well, since deleting what git ignores is reason enough.
            .Item("Discard All Changes and Ignored Files", "", () => commands.RepoCmds.CleanWorkingFolder());
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

    // Picks the row's commit into the current branch, staged for the commit dialog, or the commits of
    // the rows selected with ⇧↑↓, which the item counts, each committed as it was made
    MenuItem GetCherryPickItem(Branch? rb, Branch? cb, bool isStatusOK)
    {
        var selection = commands.View.Selection;
        var into = cb?.ShortNiceUniqueName();
        if (
            selection.IsRows
            && CommitCommands.CommitsToPick(repo.Repo, selection.I1, selection.I2) is { Count: > 1 } picks
        )
        {
            var whyNot = CommitCommands.WhyNotCherryPick(repo.Repo, selection.I1, selection.I2);
            return new MenuItem(
                $"Cherry Pick {picks.Count} Commits into {into}",
                "",
                () => cmds.CherryPick(),
                () => whyNot == ""
            )
            {
                WhyNot = () => whyNot,
            };
        }
        return new MenuItem($"Cherry Pick into {into}", "", () => cmds.CherryPick(), () => isStatusOK && rb != cb)
        {
            WhyNot = () => !isStatusOK ? Why.Changes : "The commit is on the current branch already",
        };
    }

    // The commit moved past the next one up the branch, or down it, each named by the commit it is
    // moved past, so that which way is which needs no thinking about. None when neither can be.
    IEnumerable<MenuItem> GetReorderItems(Commit c)
    {
        if (CommitRewrite.WhyNotMove(repo.Repo, c, true) != "" && CommitRewrite.WhyNotMove(repo.Repo, c, false) != "")
            return [];

        MenuItem Move(bool isUp)
        {
            var other = c.IsUncommitted ? null : CommitRewrite.Neighbour(repo.Repo, c, isUp);
            var name =
                other == null
                    ? (isUp ? "Move Up" : "Move Down")
                    : $"Move {(isUp ? "Above" : "Below")} '{Shortened(other.Subject)}'";
            var whyNot = CommitRewrite.WhyNotMove(repo.Repo, c, isUp);
            return new MenuItem(name, "", () => cmds.MoveCommit(c.Id, isUp), () => whyNot == "")
            {
                WhyNot = () => whyNot,
            };
        }
        return [Move(true), Move(false)];
    }

    static string Shortened(string subject) => subject.Length <= 30 ? subject : subject[..29] + "…";

    const string NoCommit = "The uncommitted changes are no commit: move to one first";

    // The diff of this commit with the one marked for a diff, once one is
    IEnumerable<MenuItem> GetDiffWithMarkedItems(Commit c)
    {
        var markedId = diffMark.IdIn(repo.Path);
        if (markedId == "" || !repo.Repo.CommitById.ContainsKey(markedId))
            return [];

        return Menu.Items.Item(
            $"Diff with {Sid(markedId)}",
            "",
            () => cmds.DiffWithMarked(c.Id),
            () => !c.IsUncommitted && c.Id != markedId,
            () => c.IsUncommitted ? NoCommit : "This is the commit marked: Diff with it in the menu of another one"
        );
    }

    // The commit on the row or the commits selected, which Ctrl-C, 'i' and '⇧i' copy (CommitCopy).
    // The keys are shown with the items they do the same as, and no other item has one: a menu's key
    // is what does the same from the log, where every other letter is taken.
    IEnumerable<MenuItem> GetCopyItems()
    {
        var commits = CopiedCommits();
        var copy = commands.RepoCmds;
        if (commits.Count == 0)
            return Menu.Items; // On the uncommitted row, where the commit menu greys out its Copy
        if (commits.Count > 1)
        {
            var whyNoRange = CommitCopy.WhyNoRange(commits);
            return Menu
                .Items.Item("Commit Ids", "i", () => copy.Copy(CopyKind.Ids))
                .Item("Commit Messages", "⇧i", () => copy.Copy(CopyKind.Messages))
                .Item("Ids and Subjects", "", () => copy.Copy(CopyKind.IdsAndSubjects))
                .Item(
                    $"Range {CommitCopy.RangeName(commits)}",
                    "",
                    () => copy.Copy(CopyKind.Range),
                    () => whyNoRange == "",
                    () => whyNoRange
                );
        }

        var c = commits[0];
        return Menu
            .Items.Item("Commit Id", "i", () => copy.Copy(CopyKind.Ids))
            .Item("Commit Message", "⇧i", () => copy.Copy(CopyKind.Messages))
            .Item("Subject", "", () => copy.Copy(CopyKind.Subjects))
            .Item("Id and Subject", "", () => copy.Copy(CopyKind.IdsAndSubjects))
            .Item("Short Id", "", () => copy.Copy(CopyKind.ShortIds))
            .Item(
                "Link to Commit",
                "",
                () => copy.CopyCommitLink(c.Id),
                () => WebCommands.CanOpenCommit(repo.Repo, c),
                () => WebCommands.WhyNoOpenCommit(repo.Repo, c)
            );
    }

    IReadOnlyList<Commit> CopiedCommits() => CommitCopy.Commits(repo.Repo, commands.View.Selection, repo.CurrentIndex);

    string WhyNotCopy() => CommitCopy.WhyNot(repo.Repo, commands.View.Selection, repo.CurrentIndex);

    // Squashes the commits selected with Shift-↑↓, which the item names once there are some
    IEnumerable<MenuItem> GetSquashItems()
    {
        var selection = commands.View.Selection;
        var (i1, i2) = (selection.I1, selection.I2);
        var selected = "";
        var whyNot = "Select the commits to squash with ⇧↑↓ first";
        Commit? c1 = null;
        Commit? c2 = null;
        if (selection.IsRows)
        { // User selected range of commits
            c1 = repo.Repo.ViewCommits[i1];
            c2 = repo.Repo.ViewCommits[i2];
            whyNot = CommitRewrite.WhyNotSquash(repo.Repo, c1, c2);
            if (whyNot == "")
            {
                selected = $"{CommitRewrite.Squashed(repo.Repo, c1, c2).Count} Commits";
            }
        }

        return Menu.Items.Item(
            selected == "" ? "Squash ..." : $"Squash {selected} ...",
            "",
            () => cmds.SquashCommits(c1!.Id, c2!.Id),
            () => c1 != null && whyNot == "",
            () => whyNot
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
