using gmd.Common;
using gmd.Cui.App;
using gmd.Cui.Common;
using gmd.Cui.Start;
using gmd.Server;

namespace gmd.Cui.RepoView;

interface IRepoMenu
{
    void Show(int x, int y);
    void ShowOpenRepo(int x, int y);

    IEnumerable<MenuItem> GetNewReleaseItems();
    IEnumerable<MenuItem> GetOperationItems();
    void ShowOperationMenu(int x, int y);
    IEnumerable<MenuItem> GetRepoMenuItems();
}

class RepoMenu : IRepoMenu
{
    readonly IViewRepo repo;
    readonly CommandContext commands;
    readonly IRepoCommands cmds;
    readonly Config config;
    readonly IConfigDlg configDlg;
    readonly Func<SubMenu> showBranchSubMenu;

    // The Show Branch menu is the branch menu's, which is made after this one, since it nests this one
    // at its bottom, so it is reached through a function
    public RepoMenu(
        IViewRepo repo,
        CommandContext commands,
        Config config,
        IConfigDlg configDlg,
        Func<SubMenu> showBranchSubMenu
    )
    {
        this.repo = repo;
        this.commands = commands;
        this.cmds = commands.RepoCmds;
        this.config = config;
        this.configDlg = configDlg;
        this.showBranchSubMenu = showBranchSubMenu;
    }

    public void Show(int x, int y)
    {
        Menu.Show($"Repo Menu", x, y + 2, GetRepoMenuItems());
    }

    // The config dialog can turn the key-hint line on or off, which changes the layout
    void ShowConfig()
    {
        // Turning on showing new branches shows those that come from now on, not every one that
        // came while it was off, which may be many in a busy repo: those are taken as seen
        var change = configDlg.Show(repo.Repo.Path);
        if (change.IsShowNewBranchesTurnedOn)
            commands.View.MarkHiddenNewsSeen(repo.HiddenNews);

        if (change.IsReadAgain)
        { // The integration branches changed, which the branch structure is inferred with
            commands.View.Refresh();
        }
        commands.View.UpdateLayout();
    }

    public void ShowOpenRepo(int x, int y)
    {
        Menu.Show($"Open, Clone or Init Repo", x, y + 2, GetOpenRepoItems());
    }

    public IEnumerable<MenuItem> GetRepoMenuItems()
    {
        return Menu
            .Items.Items(GetOperationItems())
            .Item(
                "Pull All Branches",
                "⇧u",
                () => commands.PushPullCmds.PullAllBranches(),
                () => BranchPushPullCommands.CanPull(repo.Repo),
                () => BranchPushPullCommands.WhyNoPull(repo.Repo)
            )
            .Item(
                "Push All Branches",
                "⇧p",
                () => commands.PushPullCmds.PushAllBranches(),
                () => BranchPushPullCommands.CanPush(repo.Repo),
                () => repo.Repo.Status.IsMerging ? Why.InProgress : "Nothing to push"
            )
            // Which branches the log shows, and which commits: what to look at, of the whole repo
            .Items([showBranchSubMenu()])
            .Item("Search ...", "f", () => cmds.SearchFilterRepo())
            .Item(
                "Next Match",
                "n",
                () => commands.BranchCmds.ShowSearchMatch(1),
                () => repo.SearchMatches.IsActive,
                () => NoSearch
            )
            .Item(
                "Previous Match",
                "⇧n",
                () => commands.BranchCmds.ShowSearchMatch(-1),
                () => repo.SearchMatches.IsActive,
                () => NoSearch
            )
            .Item("Refresh", "r", () => cmds.RefreshAndFetch())
            .Item("Worktrees ...", "w", () => commands.WorktreeCmds.ShowWorktrees())
            .Item("Delete Merged Branches ...", "", () => commands.CreateCmds.DeleteMergedBranches())
            // The files git ignores, the uncommitted changes kept: Discard All Changes, in Undo, is those
            .Item("Clean Working Folder ...", "", () => cmds.CleanWorkingFolder())
            .Item("Open Repository in Browser", "", () => cmds.OpenRepoInBrowser())
            .SubMenu("Open, Clone or Init Repo", "o", GetOpenRepoItems())
            .Item("Config ...", "", () => ShowConfig())
            .SubMenu("Help", "", GetHelpItems())
            .Item("Quit", "q, Esc", () => UI.Shutdown());
    }

    // Where to learn about gmd and to tell about it: the help, what is new in this version, a bug
    // report or a suggestion as a GitHub issue with the version and system filled in, the log a bug
    // report wants, to read or to copy into it, and About
    IEnumerable<MenuItem> GetHelpItems() =>
        Menu
            .Items.Item("Help", "?, F1", () => cmds.ShowHelp())
            .Item("What's New", "", () => cmds.ShowWhatsNew())
            .Separator()
            .Item("Report a Bug", "", () => cmds.ReportBug())
            .Item("Suggest an Improvement", "", () => cmds.SuggestImprovement())
            .Separator()
            .Item("View Log", "", () => cmds.ShowLog())
            .Item("Copy Log", "", () => cmds.CopyLog())
            .Separator()
            .Item("About", "", () => cmds.ShowAbout());

    const string NoSearch = "Search with f and pick a commit first";

    // What can be done about an operation git stopped part way through. Heads the menu because a
    // stopped rebase is the most urgent thing about the repo while it lasts, and self-hides when
    // there is none, as GetNewReleaseItems does — so nothing changes for the usual case.
    //
    // A merge has no 'Continue': committing is what finishes one, and Commit is already on the
    // commit menu with its dialog behind it. The other operations really do need telling to carry
    // on, and until now gmd could start them but not finish them.
    public IEnumerable<MenuItem> GetOperationItems()
    {
        if (repo.Repo.Status.Operation == GitOperation.None)
            return Menu.Items;

        return Menu.Items.Separator(cmds.OperationSummary()).Items(GetOperationActionItems()).Separator();
    }

    // The same items on their own, for a click on the operation in the application bar, with the
    // summary as the title rather than as a header line
    public void ShowOperationMenu(int x, int y)
    {
        if (repo.Repo.Status.Operation == GitOperation.None)
            return;
        Menu.Show(cmds.OperationSummary(), x, y + 2, GetOperationActionItems());
    }

    // Resolve first, since the conflicts are what keep the operation from being finished: it opens
    // the diff of the uncommitted changes, where Enter on a conflicted file opens the resolver
    IEnumerable<MenuItem> GetOperationActionItems()
    {
        var status = repo.Repo.Status;
        var name = cmds.OperationName();

        // Omitted rather than disabled: neither can ever apply to the operation in progress, so a
        // permanently greyed 'Continue Merge' would only suggest that continuing a merge is a thing
        // gmd might one day do. Contrast the diff view's Show More/Less Context, which are disabled
        // because they are meaningful in general and merely have nowhere to go just now.
        //
        // Continue is offered for whatever a commit does not finish, which is not the same as "not
        // a merge": gmd's own Cherry Pick and Undo/Revert Commit run '--no-commit' and are finished
        // by the commit dialog, so continuing one would commit it with git's message instead.
        var hasContinue = !status.IsFinishedByCommit;
        var hasSkip = status.Operation is GitOperation.Rebase or GitOperation.Am;

        return Menu
            .Items.Item(status.Conflicted > 0, "Resolve Conflicts", "", () => commands.RepoCmds.ResolveConflicts())
            .Item(hasContinue, $"Continue {name}", "", () => cmds.ContinueOperation())
            .Item(hasSkip, "Skip This Commit", "", () => cmds.SkipOperationCommit())
            .Item($"Abort {name}", "", () => cmds.AbortOperation());
    }

    public IEnumerable<MenuItem> GetNewReleaseItems() =>
        NewReleaseItems(config.Releases.UpdateVersion(), () => cmds.UpdateRelease());

    // The offer of a new version, as the start page makes it: under 'Update available', and green.
    // None when there is no new version, i.e. "".
    internal static IEnumerable<MenuItem> NewReleaseItems(string updateVersion, Action update)
    {
        if (updateVersion == "")
            return Menu.Items;

        return Menu
            .Items.Separator("Update available")
            .Item(new MenuItem($"Update to Gmd {updateVersion} ...", "", update) { TextColor = Color.BrightGreen })
            .Separator();
    }

    IEnumerable<MenuItem> GetOpenRepoItems() =>
        Menu
            .Items.Items(GetRecentRepoItems())
            .Separator()
            .Item("Browse ...", "", () => cmds.ShowBrowseRepoDialog())
            .Item("Clone ...", "", () => cmds.Clone())
            .Item("Init ...", "", () => cmds.InitRepo());

    // The recent repositories as the start page lists them, by name and '~/...' path, and numbered,
    // so 'o' and a number opens one. The open one is greyed out and has no number, which makes 'o 1'
    // the repository that was open before it.
    IEnumerable<MenuItem> GetRecentRepoItems()
    {
        var number = 0;
        return StartRepos
            .RecentOf(config.RecentFolders, Files.HomePath)
            .Select(recent =>
            {
                var isOpen = Files.IsSamePath(recent.Path, repo.Repo.Path);
                var shortcut = !isOpen && ++number <= 9 ? $"{number}" : "";
                return new MenuItem(recent.Name, shortcut, () => cmds.ShowRepo(recent.Path), () => !isOpen)
                {
                    Detail = recent.ShownPath,
                    WhyNot = () => "This repository is already open",
                };
            })
            .ToList();
    }
}
