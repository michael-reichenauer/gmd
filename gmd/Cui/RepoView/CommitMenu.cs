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
    readonly IRepoViewMenus repoViewMenus;
    readonly IRepo repo;
    readonly IRepoCommands cmds;

    public CommitMenu(IRepoViewMenus repoViewMenus, IRepo repo, IRepoCommands cmd)
    {
        this.repoViewMenus = repoViewMenus;
        this.repo = repo;
        this.cmds = cmd;
    }

    public void Show(int x, int y, int index)
    {
        var c = repo.Repo.ViewCommits[index];
        Menu.Show($"Commit: {RepoViewMenus.Sid(c.Id)}", x, y + 2, GetCommitMenuItems(c.Id));
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
        var sid = RepoViewMenus.Sid(c.Id);

        return Menu.Items
            .Items(repoViewMenus.GetNewReleaseItems())
            .Item("Commit ...", "C", () => cmds.CommitFromMenu(false), () => !isStatusOK)
            .Item("Amend ...", "A", () => cmds.CommitFromMenu(true), () => !isStatusOK && cc.IsAhead)
            .Item("Commit Diff ...", "D", () => cmds.ShowDiff(c.Id))
            .SubMenu("Undo", "", GetCommitUndoItems())
            .SubMenu("Stash", "", GetStashMenuItems())
            .SubMenu("Tag", "", GetTagItems(), () => c.Id != Repo.UncommittedId)
            .Item("Create Branch from Commit ...", "", () => cmds.CreateBranchFromCommit(), () => !c.IsUncommitted)
            .Item($"Merge From Commit to {cb?.ShortNiceUniqueName()}", "", () => cmds.MergeBranch(c.Id), () => isStatusOK && rb != cb)
            .Item($"Cherry Pick Commit to {cb?.ShortNiceUniqueName()}", "", () => cmds.CherryPick(c.Id), () => isStatusOK && rb != cb)
            .Item("Switch to Commit", "", () => cmds.SwitchToCommit(),
                    () => isStatusOK && repo.RowCommit.Id != repo.Repo.CurrentCommit().Id)
            .Separator()
            .SubMenu("Show/Open Branch", "Shift →", repoViewMenus.GetShowBranchItems())
            .Item("Toggle Commit Details ...", "Enter", () => cmds.ToggleDetails())
            .Item("File History ...", "", () => cmds.ShowFileHistory())
            .SubMenu("Repo Menu", "", repoViewMenus.GetRepoMenuItems());
    }


    IEnumerable<MenuItem> GetCommitUndoItems()
    {
        string id = repo.RowCommit.Id;
        var binaryPaths = repo.Repo.Status.AddedFiles
            .Concat(repo.Repo.Status.ModifiedFiles)
            .Concat(repo.Repo.Status.RenamedTargetFiles)
            .Where(f => !Files.IsText(Path.Join(repo.Repo.Path, f)))
            .ToList();

        return Menu.Items
            .SubMenu("Undo/Restore an Uncommitted File", "", GetUncommittedFileItems(), () => cmds.CanUndoUncommitted())
            .Item($"Undo Commit", "", () => cmds.UndoCommit(id), () => repo.Repo.Status.IsOk)
            .Item($"Uncommit", "", () => cmds.UncommitLastCommit(), () => cmds.CanUncommitLastCommit())
            .Separator()
            .Item("Undo/Restore all Uncommitted Binary Files", "", () => cmds.UndoUncommittedFiles(binaryPaths), () => binaryPaths.Any())
            .Item("Undo/Restore all Uncommitted Changes", "",
                () => cmds.UndoAllUncommittedChanged(), () => cmds.CanUndoUncommitted());
    }

    IEnumerable<MenuItem> GetStashMenuItems() => Menu.Items
        .Item("Stash Changes", "", () => cmds.Stash(), () => !repo.Repo.Status.IsOk)
        .SubMenu("Stash Pop", "", GetStashPopItems(), () => repo.Repo.Status.IsOk)
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
}
