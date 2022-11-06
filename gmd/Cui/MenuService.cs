using gmd.ViewRepos;
using NStack;
using Terminal.Gui;

namespace gmd.Cui;

interface IMenuService
{
    void SetRepoViewer(IRepoView repoView);
    void ShowMainMenu(Repo repo, int middleX);
    void ShowShowBranchesMenu(Repo repo, Point point);
    void ShowHideBranchesMenu(Repo repo, Point point);
}

class MenuService : IMenuService
{
    readonly IViewRepoService viewRepoService;
    IRepoView? repoViewer;

    IRepoView repoView => repoViewer!;

    internal MenuService(IViewRepoService viewRepoService)
    {
        this.viewRepoService = viewRepoService;
    }

    public void SetRepoViewer(IRepoView repoView)
    {
        this.repoViewer = repoView;
    }

    public void ShowMainMenu(Repo repo, int middleX)
    {
        List<MenuItem> items = new List<MenuItem>();
        items.Add(new MenuBarItem("Show/Scroll to Branch", GetShowBranchItems(repo)));

        var menu = new ContextMenu(middleX - 10, 0, new MenuBarItem(items.ToArray()));
        menu.Show();
    }

    public void ShowShowBranchesMenu(Repo repo, Point point)
    {
        List<MenuItem> items = new List<MenuItem>();
        var scrollToItems = GetScrollToItems();
        var switchToItems = GetSwitchToItems();

        if (scrollToItems.Length > 0)
        {
            items.Add(Separator("Scroll to"));
            items.AddRange(scrollToItems);
        }
        if (switchToItems.Length > 0)
        {
            items.Add(Separator("Switch to"));
            items.AddRange(switchToItems);
        }

        if (items.Count > 0)
        {
            items.Add(Separator("More"));
        }

        items.Add(new MenuBarItem("Show Branch", GetShowBranchItems(repo)));
        items.Add(new MenuBarItem("Main Menu", GetMainMenuItems()));

        var menu = new ContextMenu(point.X, point.Y, new MenuBarItem(items.ToArray()));
        menu.Show();
    }

    public void ShowHideBranchesMenu(Repo repo, Point point)
    {
        List<MenuItem> items = new List<MenuItem>();

        items.Add(Separator("Hide"));
        items.AddRange(GetHideItems(repo));

        var menu = new ContextMenu(point.X, point.Y, new MenuBarItem(items.ToArray()));
        menu.Show();
    }


    private MenuItem[] GetScrollToItems()
    {
        return new MenuItem[0];
    }

    private MenuItem[] GetSwitchToItems()
    {
        return new MenuItem[0];
    }

    private MenuItem[] GetHideItems(Repo repo)
    {
        var branches = repo.Branches
            .Where(b => !b.IsMainBranch)
            .DistinctBy(b => b.DisplayName)
            .OrderBy(b => b.DisplayName);

        return branches.Select(b =>
            new MenuItem(b.DisplayName, "", () => HideBranch(repo, b.Name)))
            .ToArray();
    }


    MenuItem[] GetShowBranchItems(Repo repo)
    {
        List<MenuItem> items = new List<MenuItem>();

        var allBranches = viewRepoService.GetAllBranches(repo);

        var liveBranches = allBranches
            .Where(b => b.IsGitBranch)
            .DistinctBy(b => b.DisplayName)
            .OrderBy(b => b.DisplayName);

        var liveAndDeletedBranches = allBranches
            .DistinctBy(b => b.DisplayName)
            .OrderBy(b => b.DisplayName);

        var recentBranches = liveAndDeletedBranches
            .OrderBy(b => repo.AugmentedRepo.CommitById[b.TipId].Index)
            .Take(15);

        items.Add(new MenuBarItem("Recent Branches", ToShowBranchesItems(repo, recentBranches)));
        items.Add(new MenuBarItem("Live Branches", ToShowBranchesItems(repo, liveBranches)));
        items.Add(new MenuBarItem("Live and Deleted Branches", ToShowBranchesItems(repo, liveAndDeletedBranches)));

        return items.ToArray();
    }

    MenuItem[] ToShowBranchesItems(Repo repo, IEnumerable<Branch> branches)
    {
        return branches.Select(b =>
            new MenuItem(b.DisplayName, "", () => ShowBranch(repo, b.Name)))
            .ToArray();
    }

    void ShowBranch(Repo repo, string name)
    {
        RunInBackground(async () =>
        {
            Repo newRepo = await viewRepoService.ShowBranch(repo, name);
            repoView.ShowRepo(newRepo);

        });
    }

    void HideBranch(Repo repo, string name)
    {
        RunInBackground(async () =>
        {
            Repo newRepo = await viewRepoService.HideBranch(repo, name);
            repoView.ShowRepo(newRepo);
        });
    }

    private void RunInBackground(Func<Task> doTask)
    {
        doTask().RunInBackground();
    }

    MenuItem[] GetShowScrollBranchItems()
    {
        List<MenuItem> items = new List<MenuItem>();
        var scrollToItems = GetScrollToItems();
        var switchToItems = GetSwitchToItems();

        if (scrollToItems.Length > 0)
        {
            items.Add(Separator("Scroll to"));
            items.AddRange(switchToItems);
        }
        if (items.Count > 0)
        {
            items.Add(Separator("Show"));
        }


        return items.ToArray();
    }

    MenuItem[] GetMainMenuItems()
    {
        List<MenuItem> supportedCultures = new List<MenuItem>();
        return supportedCultures.ToArray();
    }


    MenuItem Separator(string text)
    {
        const int maxDivider = 20;
        text = text.Max(maxDivider - 1);
        string suffix = new string('─', maxDivider - text.Length);
        return new MenuItem($"── {text} {suffix}", "", () => { }, () => false);
    }
}

//  var menu = new ContextMenu(point.X, point.Y, new MenuBarItem(new MenuItem[] {
//         Divider("Scroll to"),
//         new MenuItem ("_Configuration", "", () => MessageBox.Query (50, 5, "Info", "This would open settings dialog", "Ok")),
//         Divider("Switch to"),
//         new MenuBarItem ("More options", new MenuItem [] {
//             new MenuItem ("_Setup", "", () => MessageBox.Query (50, 5, "Info", "This would open setup dialog", "Ok")),
//             new MenuItem ("_Maintenance", "", () => MessageBox.Query (50, 5, "Info", "This would open maintenance dialog", "Ok")),
//         }),
//         new MenuBarItem ("_Languages", GetSupportedCultures ()),
//         new MenuItem ("_Quit", "", UI.Shutdown)
//      }));
