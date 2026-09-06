using gmd.Common;
using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.RepoView;

interface IRepoMenu
{
    void Show(int x, int y);
    void ShowOpenRepo(int x, int y);

    IEnumerable<MenuItem> GetNewReleaseItems();
    IEnumerable<MenuItem> GetOperationItems();
    IEnumerable<MenuItem> GetRepoMenuItems();
}

class RepoMenu : IRepoMenu
{
    readonly IViewRepo repo;
    readonly IRepoCommands cmds;
    readonly Config config;
    readonly IConfigDlg configDlg;

    public RepoMenu(IViewRepo repo, IRepoCommands cmd, Config config, IConfigDlg configDlg)
    {
        this.repo = repo;
        this.cmds = cmd;
        this.config = config;
        this.configDlg = configDlg;
    }

    public void Show(int x, int y)
    {
        Menu.Show($"Repo Menu", x, y + 2, GetRepoMenuItems());
    }

    public void ShowOpenRepo(int x, int y)
    {
        Menu.Show($"Open/Clone/Init Repo", x, y + 2, GetOpenRepoItems());
    }

    public IEnumerable<MenuItem> GetRepoMenuItems()
    {
        var isStatusOK = repo.Repo.Status.IsOk;

        return Menu
            .Items.Items(GetOperationItems())
            .Item("Pull/Update All Branches", "Shift-U", () => repo.BranchCmds.PullAllBranches(), () => isStatusOK)
            .Item("Push All Branches", "Shift-P", () => repo.BranchCmds.PushAllBranches(), () => isStatusOK)
            .Item("Search/Filter ...", "F", () => cmds.SearchFilterRepo())
            .Item("Refresh/Reload", "R", () => cmds.RefreshAndFetch())
            .Item("Clean/Restore Working Folder", "", () => cmds.CleanWorkingFolder())
            .Item("Worktrees ...", "W", () => repo.BranchCmds.ShowWorktrees())
            .SubMenu("Open/Clone/Init Repo", "O", GetOpenRepoItems())
            .Item("Config ...", "", () => configDlg.Show(repo.Repo.Path))
            .Item("Help ...", "?, F1", () => cmds.ShowHelp())
            .Item("About ...", "", () => cmds.ShowAbout())
            .Item("Quit", "Q, Esc", () => UI.Shutdown());
    }

    // What can be done about an operation git stopped part way through. Heads the menu because a
    // stopped rebase is the most urgent thing about the repo while it lasts, and self-hides when
    // there is none, as GetNewReleaseItems does — so nothing changes for the usual case.
    //
    // A merge has no 'Continue': committing is what finishes one, and Commit is already on the
    // commit menu with its dialog behind it. The other operations really do need telling to carry
    // on, and until now gmd could start them but not finish them.
    public IEnumerable<MenuItem> GetOperationItems()
    {
        var status = repo.Repo.Status;
        if (status.Operation == GitOperation.None)
            return Menu.Items;

        var name = cmds.OperationName();

        // Omitted rather than disabled: neither can ever apply to the operation in progress, so a
        // permanently greyed 'Continue Merge' would only suggest that continuing a merge is a thing
        // gmd might one day do. Contrast the diff view's More/Less Context, which are disabled
        // because they are meaningful in general and merely have nowhere to go just now.
        //
        // Continue is offered for whatever a commit does not finish, which is not the same as "not
        // a merge": gmd's own Cherry Pick and Undo/Revert Commit run '--no-commit' and are finished
        // by the commit dialog, so continuing one would commit it with git's message instead.
        var hasContinue = !status.IsFinishedByCommit;
        var hasSkip = status.Operation is GitOperation.Rebase or GitOperation.Am;

        return Menu
            .Items.Separator(cmds.OperationSummary())
            .Item(hasContinue, $"Continue {name}", "", () => cmds.ContinueOperation())
            .Item(hasSkip, "Skip This Commit", "", () => cmds.SkipOperationCommit())
            .Item($"Abort {name}", "", () => cmds.AbortOperation())
            .Separator();
    }

    public IEnumerable<MenuItem> GetNewReleaseItems()
    {
        if (!config.Releases.IsUpdateAvailable())
            return Menu.Items;
        return Menu
            .Items.Separator("New Release Available !!!")
            .Item("Update to Latest Version ...", "", () => cmds.UpdateRelease())
            .Separator();
    }

    IEnumerable<MenuItem> GetOpenRepoItems() =>
        Menu
            .Items.Items(GetRecentRepoItems())
            .Separator()
            .Item("Browse ...", "", () => cmds.ShowBrowseRepoDialog())
            .Item("Clone ...", "", () => cmds.Clone())
            .Item("Init ...", "", () => cmds.InitRepo());

    IEnumerable<MenuItem> GetRecentRepoItems() =>
        config
            .RecentFolders.Where(Directory.Exists)
            .Take(Config.MaxRecentFolders)
            .Select(path => Menu.Item(path, "", () => cmds.ShowRepo(path), () => path != repo.Repo.Path));
}
