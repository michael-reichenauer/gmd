using gmd.Common;
using gmd.Cui.Common;

namespace gmd.Cui.RepoView;


interface IRepoMenu
{
    void Show(int x, int y);

    IEnumerable<MenuItem> GetNewReleaseItems();
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

    public IEnumerable<MenuItem> GetRepoMenuItems()
    {
        var isStatusOK = repo.Repo.Status.IsOk;

        return Menu.Items
            .Item("Pull/Update All Branches", "Shift-U", () => repo.BranchCmds.PullAllBranches(), () => isStatusOK)
            .Item("Push All Branches", "Shift-P", () => repo.BranchCmds.PushAllBranches(), () => isStatusOK)
            .Item("Search/Filter ...", "F", () => cmds.Filter())
            .Item("Refresh/Reload", "R", () => cmds.RefreshAndFetch())
            .Item("Clean/Restore Working Folder", "", () => cmds.CleanWorkingFolder())
            .SubMenu("Open/Clone/Init Repo", "O", GetOpenRepoItems())
            .Item("Config ...", "", () => configDlg.Show(repo.Repo.Path))
            .Item("Help ...", "?, F1", () => cmds.ShowHelp())
            .Item("About ...", "", () => cmds.ShowAbout())
            .Item("Quit", "Q, Esc", () => UI.Shutdown());
    }


    public IEnumerable<MenuItem> GetNewReleaseItems()
    {
        if (!config.Releases.IsUpdateAvailable()) return Menu.Items;
        return Menu.Items
           .Separator("New Release Available !!!")
           .Item("Update to Latest Version ...", "", () => cmds.UpdateRelease())
           .Separator();
    }


    IEnumerable<MenuItem> GetOpenRepoItems() => Menu.Items
        .Items(GetRecentRepoItems())
        .Separator()
        .Item("Browse ...", "", () => cmds.ShowBrowseDialog())
        .Item("Clone ...", "", () => cmds.Clone())
        .Item("Init ...", "", () => cmds.InitRepo());


    IEnumerable<MenuItem> GetRecentRepoItems() =>
        config.RecentFolders
            .Where(Directory.Exists)
            .Take(10)
            .Select(path => Menu.Item(path, "", () => cmds.ShowRepo(path), () => path != repo.Repo.Path));
}
