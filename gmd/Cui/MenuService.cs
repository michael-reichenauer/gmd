using gmd.ViewRepos;
using Terminal.Gui;

namespace gmd.Cui;


interface IMenuService
{
    void ShowMainMenu(IRepo repo);
    void ShowShowBranchesMenu(IRepo repo);
    void ShowHideBranchesMenu(IRepo repo);
}

class MenuService : IMenuService
{
    readonly IViewRepoService viewRepoService;

    internal MenuService(IViewRepoService viewRepoService)
    {
        this.viewRepoService = viewRepoService;
    }

    public void ShowMainMenu(IRepo repo)
    {
        List<MenuItem> items = new List<MenuItem>();

        items.Add(Separator($"Commit {Sid(repo.Repo.Commits[repo.CurrentIndex].Id)}"));
        items.Add(new MenuItem("Commit ...", "",
            () => repo.Commit(),
            () => !repo.Repo.Status.IsOk));

        items.Add(Separator("Branches"));
        items.Add(new MenuBarItem("Show Branch", GetShowBranchItems(repo)));
        items.Add(new MenuBarItem("Hide Branch", GetHideItems(repo)));
        items.Add(new MenuBarItem("Push", GetPushItems(repo)));

        var menu = new ContextMenu(repo.ContentWidth / 2 - 10, 0, new MenuBarItem(items.ToArray()));
        menu.Show();
    }

    string Sid(string id) => id == Repo.UncommittedId ? "uncommitted" : id.Substring(0, 6);

    public void ShowShowBranchesMenu(IRepo repo)
    {
        List<MenuItem> items = new List<MenuItem>();
        var showItems = GetShowItems(repo, repo.CurrentIndex);
        var scrollToItems = GetScrollToItems();
        var switchToItems = GetSwitchToItems();


        if (showItems.Length > 0)
        {
            items.Add(Separator("Show"));
            items.AddRange(showItems);
        }

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

        var menu = new ContextMenu(repo.CurrentPoint.X, repo.CurrentPoint.Y, new MenuBarItem(items.ToArray()));
        menu.Show();
    }

    public void ShowHideBranchesMenu(IRepo repo)
    {
        List<MenuItem> items = new List<MenuItem>();

        items.Add(Separator("Hide"));
        items.AddRange(GetHideItems(repo));

        var menu = new ContextMenu(repo.CurrentPoint.X, repo.CurrentPoint.Y, new MenuBarItem(items.ToArray()));
        menu.Show();
    }

    MenuItem[] GetPushItems(IRepo repo) =>
      repo.CanPush()
          ? new[]{
                new MenuItem("Push Current Branch", "",
                    () => repo.PushCurrentBranch(),
                    () => repo.CanPushCurrentBranch())
              }
          : new MenuItem[0];


    private MenuItem[] GetShowItems(IRepo repo, int currentIndex)
    {
        var selectedCommit = repo.Repo.Commits[currentIndex];
        var branches = viewRepoService.GetCommitBranches(repo.Repo, selectedCommit.Id);
        return ToShowBranchesItems(repo, branches);
    }


    private MenuItem[] GetScrollToItems()
    {
        return new MenuItem[0];
    }

    private MenuItem[] GetSwitchToItems()
    {
        return new MenuItem[0];
    }

    private MenuItem[] GetHideItems(IRepo repo)
    {
        var branches = repo.Repo.Branches
            .Where(b => !b.IsMainBranch)
            .DistinctBy(b => b.DisplayName)
            .OrderBy(b => b.DisplayName);

        return branches.Select(b =>
            new MenuItem(b.DisplayName, "", () => repo.HideBranch(b.Name)))
            .ToArray();
    }


    MenuItem[] GetShowBranchItems(IRepo repo)
    {
        List<MenuItem> items = new List<MenuItem>();

        var allBranches = viewRepoService.GetAllBranches(repo.Repo);

        var liveBranches = allBranches
            .Where(b => b.IsGitBranch)
            .DistinctBy(b => b.DisplayName)
            .OrderBy(b => b.DisplayName);

        var liveAndDeletedBranches = allBranches
            .DistinctBy(b => b.DisplayName)
            .OrderBy(b => b.DisplayName);

        var recentBranches = liveAndDeletedBranches
            .OrderBy(b => repo.Repo.AugmentedRepo.CommitById[b.TipId].Index)
            .Take(15);

        items.Add(new MenuBarItem("Recent Branches", ToShowBranchesItems(repo, recentBranches)));
        items.Add(new MenuBarItem("Live Branches", ToShowBranchesItems(repo, liveBranches)));
        items.Add(new MenuBarItem("Live and Deleted Branches", ToShowBranchesItems(repo, liveAndDeletedBranches)));

        return items.ToArray();
    }

    MenuItem[] ToShowBranchesItems(IRepo repo, IEnumerable<Branch> branches)
    {
        return branches.Select(b =>
            new MenuItem(b.DisplayName, "", () => repo.ShowBranch(b.Name)))
            .ToArray();
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
