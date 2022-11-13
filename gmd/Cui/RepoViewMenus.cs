using gmd.ViewRepos;
using NStack;
using Terminal.Gui;

namespace gmd.Cui;


interface IRepoViewMenus
{
    void ShowMainMenu();
    void ShowShowBranchesMenu();
    void ShowHideBranchesMenu();
}

class RepoViewMenus : IRepoViewMenus
{
    readonly IRepo repo;

    internal RepoViewMenus(IRepo repo)
    {
        this.repo = repo;
    }

    public void ShowMainMenu()
    {
        List<MenuItem> items = new List<MenuItem>();

        items.Add(UI.MenuSeparator($"Commit {Sid(repo.Repo.Commits[repo.CurrentIndex].Id)}"));
        items.Add(new MenuItem("Commit ...", "",
            () => repo.Commit(),
            () => !repo.Repo.Status.IsOk));

        items.Add(UI.MenuSeparator("Branches"));
        items.Add(new MenuBarItem("Show Branch", GetShowBranchItems()));
        items.Add(new MenuBarItem("Hide Branch", GetHideItems()));
        items.Add(new MenuBarItem("Push", GetPushItems()));
        items.Add(new MenuBarItem("Switch/Checkout", GetSwitchToItems()));
        items.Add(new MenuBarItem("Merge", GetMergeItems()));

        var menu = new ContextMenu(repo.ContentWidth / 2 - 10, 0, new MenuBarItem(items.ToArray()));
        menu.Show();
    }



    string Sid(string id) => id == Repo.UncommittedId ? "uncommitted" : id.Substring(0, 6);

    public void ShowShowBranchesMenu()
    {
        List<MenuItem> items = new List<MenuItem>();
        var showItems = GetShowItems();
        var scrollToItems = GetScrollToItems();
        var switchToItems = GetSwitchToItems();


        if (showItems.Length > 0)
        {
            items.Add(UI.MenuSeparator("Show"));
            items.AddRange(showItems);
        }

        if (scrollToItems.Length > 0)
        {
            items.Add(UI.MenuSeparator("Scroll to"));
            items.AddRange(scrollToItems);
        }
        if (switchToItems.Length > 0)
        {
            items.Add(UI.MenuSeparator("Switch to"));
            items.AddRange(switchToItems);
        }

        if (items.Count > 0)
        {
            items.Add(UI.MenuSeparator("More"));
        }

        items.Add(new MenuBarItem("Show Branch", GetShowBranchItems()));
        items.Add(new MenuBarItem("Main Menu", GetMainMenuItems()));

        var menu = new ContextMenu(repo.CurrentPoint.X, repo.CurrentPoint.Y, new MenuBarItem(items.ToArray()));
        menu.Show();
    }

    public void ShowHideBranchesMenu()
    {
        List<MenuItem> items = new List<MenuItem>();

        items.Add(UI.MenuSeparator("Hide"));
        items.AddRange(GetHideItems());

        var menu = new ContextMenu(repo.CurrentPoint.X, repo.CurrentPoint.Y, new MenuBarItem(items.ToArray()));
        menu.Show();
    }

    MenuItem[] GetPushItems() =>
      repo.CanPush()
          ? new[]{
                new MenuItem("Push Current Branch", "",
                    () => repo.PushCurrentBranch(),
                    () => repo.CanPushCurrentBranch())
              }
          : new MenuItem[0];

    MenuItem[] GetShowItems()
    {

        var branches = repo
            .GetCommitBranches()
            .Concat(repo.Repo.Branches);

        return ToShowBranchesItems(branches, true);
    }


    MenuItem[] GetScrollToItems()
    {
        return new MenuItem[0];
    }

    MenuItem[] GetSwitchToItems()
    {
        var currentName = repo.CurrentBranch?.CommonName ?? "";
        var branches = repo.Repo.Branches
             .Where(b => b.CommonName != currentName)
             .DistinctBy(b => b.CommonName)
             .OrderBy(b => b.CommonName);

        return branches.Select(b =>
            new MenuItem(b.DisplayName, "", () => repo.SwitchTo(b.Name)))
            .ToArray();
    }

    MenuItem[] GetMergeItems()
    {
        if (repo.HasUncommittedChanges)
        {
            return new MenuItem[0];
        }

        var currentName = repo.CurrentBranch?.CommonName ?? "";
        var branches = repo.Repo.Branches
             .Where(b => b.CommonName != currentName)
             .DistinctBy(b => b.CommonName)
             .OrderBy(b => b.CommonName);

        return branches.Select(b =>
            new MenuItem(b.DisplayName, "", () => repo.MergeBranch(b.Name)))
            .ToArray();
    }



    MenuItem[] GetHideItems()
    {
        var branches = repo.Repo.Branches
            .Where(b => !b.IsMainBranch)
            .DistinctBy(b => b.CommonName)
            .OrderBy(b => b.CommonName);

        return branches.Select(b =>
            new MenuItem(b.DisplayName, "", () => repo.HideBranch(b.Name)))
            .ToArray();
    }


    MenuItem[] GetShowBranchItems()
    {
        List<MenuItem> items = new List<MenuItem>();

        var allBranches = repo.GetAllBranches();

        var liveBranches = allBranches
            .Where(b => b.IsGitBranch)
            .DistinctBy(b => b.CommonName)
            .OrderBy(b => b.CommonName);

        var liveAndDeletedBranches = allBranches
            .DistinctBy(b => b.CommonName)
            .OrderBy(b => b.CommonName);

        var recentBranches = liveAndDeletedBranches
            .OrderBy(b => repo.Repo.AugmentedRepo.CommitById[b.TipId].Index)
            .Take(15);

        items.Add(new MenuBarItem("Recent Branches", ToShowBranchesItems(recentBranches)));
        items.Add(new MenuBarItem("Live Branches", ToShowBranchesItems(liveBranches)));
        items.Add(new MenuBarItem("Live and Deleted Branches", ToShowBranchesItems(liveAndDeletedBranches)));

        return items.ToArray();
    }

    MenuItem[] ToShowBranchesItems(IEnumerable<Branch> branches, bool canBeOutside = false)
    {
        var cic = repo.CurrentIndexCommit;
        return branches
            .DistinctBy(b => b.CommonName)
            .Select(b => new MenuItem(ToShowName(b, cic, canBeOutside), "", () => repo.ShowBranch(b.Name)))
            .ToArray();
    }

    string ToShowName(Branch branch, Commit cic, bool canBeOutside)
    {
        bool isBranchIn = false;
        bool isBranchOut = false;
        if (canBeOutside &&
            !repo.Repo.BranchByName.TryGetValue(branch.Name, out var _))
        {
            if (repo.Repo.AugmentedRepo.BranchByName.TryGetValue(branch.Name, out var b))
            {
                // The branch is not shown, but does exist
                if (cic.ParentIds.Count > 1 && cic.ParentIds[1] == b.TipId)
                {
                    isBranchIn = true;
                }
                else if (cic.ChildIds.Contains(b.BottomId))
                {
                    isBranchOut = true;
                }
            }
        }

        string name = branch.DisplayName;
        name = branch.IsGitBranch ? " " + name : "~" + name;
        name = isBranchIn ? "╮" + name : name;
        name = isBranchOut ? "╯" + name : name;
        name = isBranchIn || isBranchOut ? name : " " + name;
        name = branch.IsCurrent || branch.IsLocalCurrent ? "●" + name : " " + name;

        return name;
    }

    MenuItem[] GetShowScrollBranchItems()
    {
        List<MenuItem> items = new List<MenuItem>();
        var scrollToItems = GetScrollToItems();
        var switchToItems = GetSwitchToItems();

        if (scrollToItems.Length > 0)
        {
            items.Add(UI.MenuSeparator("Scroll to"));
            items.AddRange(switchToItems);
        }
        if (items.Count > 0)
        {
            items.Add(UI.MenuSeparator("Show"));
        }

        return items.ToArray();
    }

    MenuItem[] GetMainMenuItems()
    {
        List<MenuItem> supportedCultures = new List<MenuItem>();
        return supportedCultures.ToArray();
    }
}
