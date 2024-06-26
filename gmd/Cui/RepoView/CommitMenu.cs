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

        return Menu.Items
            .Items(repoMenu.GetNewReleaseItems())
            .Item("Commit ...", "C", () => cmds.CommitFromMenu(false), () => !isStatusOK)
            .Item("Amend ...", "A", () => cmds.CommitFromMenu(true), () => !isStatusOK && cc.IsAhead)
            .Item($"Commit Diff ...", "D", () => cmds.ShowCurrentRowDiff())
            .SubMenu("Undo", "", GetCommitUndoItems())
            .SubMenu("Rebase", "", GetRebaseMenuItems())
            .SubMenu("Stash", "", GetStashMenuItems())
            .SubMenu("Tag", "", GetTagItems(), () => c.Id != Repo.UncommittedId)
            .Item("Create Branch from Commit ...", "B", () => repo.BranchCmds.CreateBranchFromCommit(), () => !c.IsUncommitted)
            .Item($"Merge From Commit to {cb?.ShortNiceUniqueName()}", "", () => repo.BranchCmds.MergeBranch(c.Id), () => isStatusOK && rb != cb)
            .Item($"Cherry Pick Commit to {cb?.ShortNiceUniqueName()}", "", () => cmds.CherryPick(), () => isStatusOK && rb != cb)
            .Item("Switch/Checkout to Commit", "", () => repo.BranchCmds.SwitchToCommit(),
                    () => isStatusOK && repo.RowCommit.Id != repo.Repo.CurrentCommit().Id)
            .Separator()
            // .SubMenu("Branches Menus", "", GetBranchesMenusItems())
            .SubMenu("Show/Open Branch", "Shift →", branchMenu.GetShowBranchItems())
            .Item("Hide All Branches", "", () => repo.BranchCmds.HideBranch("", true))
            .Item("Toggle Commit Details ...", "Enter", () => cmds.ToggleDetails())
            .Item("File History ...", "", () => cmds.ShowFileHistory())
            .SubMenu("Repo Menu", "", repoMenu.GetRepoMenuItems());
    }

    IEnumerable<MenuItem> GetBranchesMenusItems() =>
        repo.Repo.ViewBranches
        .DistinctBy(b => b.PrimaryName)
        .Select(b => Menu.SubMenu(b.NiceNameUnique, "", branchMenu.GetBranchMenuItems(b.PrimaryName, true)));


    IEnumerable<MenuItem> GetCommitUndoItems()
    {
        string id = repo.RowCommit.Id;
        var binaryPaths = repo.Repo.Status.AddedFiles
            .Concat(repo.Repo.Status.ModifiedFiles)
            .Concat(repo.Repo.Status.RenamedTargetFiles)
            .Where(f => !Files.IsText(Path.Join(repo.Path, f)))
            .ToList();

        return Menu.Items
            .SubMenu("Undo/Restore an Uncommitted File", "", GetUncommittedFileItems(), () => cmds.CanUndoUncommitted())
            .Item($"Undo Commit", "", () => cmds.UndoCommit(id), () => repo.Repo.Status.IsOk)
            .Item($"Uncommit", "", () => cmds.UncommitLastCommit(), () => cmds.CanUncommitLastCommit())
            .Item($"Uncommit until {id.Sid()}", "", () => cmds.UncommitUntilCommit(id),
                () => repo.Repo.Status.IsOk && (repo.RowBranch.IsCurrent || repo.RowBranch.IsLocalCurrent))
            .Separator()
            .Item("Undo/Restore all Uncommitted Binary Files", "", () => cmds.UndoUncommittedFiles(binaryPaths), () => binaryPaths.Any())
            .Item("Undo/Restore all Uncommitted Changes", "",
                () => repo.Cmds.UndoAllUncommittedChanged(), () => cmds.CanUndoUncommitted());
    }

    IEnumerable<MenuItem> GetRebaseMenuItems()
    {
        var selection = repo.RepoView.Selection;
        var (i1, i2) = (selection.I1, selection.I2);
        var selected = "";
        Commit? c1 = null;
        Commit? c2 = null;
        if (!selection.IsEmpty && i2 - i1 > 0)
        {   // User selected range of commits
            c1 = repo.Repo.ViewCommits[i1];
            c2 = repo.Repo.ViewCommits[i2];
            if (!c1.IsUncommitted && !c1.IsUncommitted)
            {
                selected = $"{Sid(c1.Id)}...{Sid(c2.Id)}";
            }
        }

        return Menu.Items
            .Item($"Squash {selected}", "", () => cmds.SquashCommits(c1!.Id, c2!.Id),
                () => !selection.IsEmpty && selected != "" && repo.Status.IsOk);
    }

    IEnumerable<MenuItem> GetStashMenuItems() => Menu.Items
        .Item("Stash Changes", "", () => cmds.Stash(), () => !repo.Status.IsOk)
        .SubMenu("Stash Pop", "", GetStashPopItems(), () => repo.Status.IsOk)
        .SubMenu("Stash Diff", "", GetStashDiffItems())
        .SubMenu("Stash Drop", "", GetStashDropItems());


    IEnumerable<MenuItem> GetTagItems() => Menu.Items
        .Item("Add Tag ...", "T", () => cmds.AddTag(), () => !repo.RowCommit.IsUncommitted)
        .SubMenu("Remove Tag", "", GetDeleteTagItems());

    IEnumerable<MenuItem> GetStashPopItems() => repo.Repo.Stashes.Select(s =>
        Menu.Item($"{s.Message}", "", () => cmds.StashPop(s.Name)));

    IEnumerable<MenuItem> GetStashDropItems() => repo.Repo.Stashes.Select(s =>
        Menu.Item($"{s.Message}", "", () => cmds.StashDrop(s.Name)));

    IEnumerable<MenuItem> GetStashDiffItems() => repo.Repo.Stashes.Select(s =>
        Menu.Item($"{s.Message}", "", () => cmds.StashDiff(s.Name)));


    IEnumerable<MenuItem> GetDeleteTagItems()
    {
        var commit = repo.RowCommit;
        return commit.Tags.Select(t => Menu.Item(t.Name, "", () => cmds.DeleteTag(t.Name)));
    }

    IEnumerable<MenuItem> GetUncommittedFileItems() =>
        repo.Repo.GetUncommittedFiles().Select(f => Menu.Item(f, "", () => cmds.UndoUncommittedFile(f)));

    public static string Sid(string id) => id == Repo.UncommittedId ? "uncommitted" : id.Sid();
}
