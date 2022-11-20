using gmd.Cui.Common;
using gmd.Server;
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
        var menu = new ContextMenu(repo.ContentWidth / 2 - 10, 0, new MenuBarItem(GetMainMenuItems()));
        menu.Show();
    }

    public void ShowShowBranchesMenu()
    {
        List<MenuItem> items = new List<MenuItem>();
        var showItems = GetShowItems();
        var switchToItems = GetSwitchToItems();


        if (showItems.Length > 0)
        {
            items.Add(UI.MenuSeparator("Show"));
            items.AddRange(showItems);
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

        items.Add(SubMenu("Show Branch", "", GetShowBranchItems()));
        items.Add(SubMenu("Main Menu", "M", GetMainMenuItems()));

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

    MenuItem[] GetMainMenuItems()
    {
        List<MenuItem> items = new List<MenuItem>();

        items.Add(UI.MenuSeparator($"Commit {Sid(repo.CurrentIndexCommit.Id)}"));
        items.Add(Item("Toggle Details ...", "Enter", () => repo.ToggleDetails()));
        items.Add(Item("Commit ...", "C",
            () => repo.Commit(),
            () => !repo.Repo.Status.IsOk));
        items.Add(Item("Commit Diff ...", "D", () => repo.ShowCurrentRowDiff()));

        items.Add(UI.MenuSeparator("Branches"));
        items.Add(SubMenu("Show Branch", "->", GetShowBranchItems()));
        items.Add(SubMenu("Hide Branch", "<-", GetHideItems()));
        items.Add(SubMenu("Switch/Checkout", "", GetSwitchToItems()));
        items.Add(SubMenu("Push", "", GetPushItems(), () => repo.CanPush()));
        items.Add(SubMenu("Update/Pull", "", GetPullItems(), () => repo.CanPull()));
        items.Add(SubMenu("Merge", "", GetMergeItems()));
        items.Add(Item("Create Branch ...", "B", repo.CreateBranch));
        items.Add(Item("Create Branch from commit ...", "",
          () => repo.CreateBranchFromCommit(), () => repo.Repo.Status.IsOk));
        items.Add(SubMenu("Delete Branch", "", GetDeleteItems()));

        items.Add(UI.MenuSeparator("More"));
        items.Add(Item("Seach/Filter ...", "F", () => repo.Filter()));
        items.Add(Item("About ...", "", () => repo.ShowAbout()));
        items.Add(Item("Quit", "Esc", () => UI.Shutdown()));

        return items.ToArray();
    }

    MenuBarItem SubMenu(string title, string key, MenuItem[] children, Func<bool>? canExecute = null) =>
        new MenuBarItem(title, key == "" ? "" : key + " ", null, canExecute) { Children = children };

    MenuItem Item(string title, string key, Action action, Func<bool>? canExecute = null) =>
        new MenuItem(title, key == "" ? "" : key + " ", action, canExecute);

    MenuItem[] GetPushItems()
    {
        List<MenuItem> items = new List<MenuItem>();

        if (repo.CurrentBranch != null)
        {
            items.Add(Item(ToShowName(repo.CurrentBranch), "P",
                    () => repo.PushCurrentBranch(),
                    () => repo.CanPushCurrentBranch()));
        }
        items.AddRange(repo.GetShownBranches()
            .Where(b => !b.IsRemote && !b.IsCurrent && b.HasLocalOnly && !b.HasRemoteOnly)
            .Select(b => (Item($"Push {ToShowName(b)}", "", () => repo.PushBranch(b.Name)))));

        return items.ToArray();
    }


    MenuItem[] GetPullItems()
    {
        List<MenuItem> items = new List<MenuItem>();
        if (repo.CurrentBranch != null)
        {
            items.Add(Item(ToShowName(repo.CurrentBranch), "U",
                    () => repo.PullCurrentBranch(),
                    () => repo.CanPullCurrentBranch()));
        }
        items.AddRange(repo.GetShownBranches()
        .Where(b => b.IsRemote && !b.IsCurrent && b.HasRemoteOnly)
        .Select(b => (Item($"Pull {ToShowName(b)}", "", () => repo.PullBranch(b.Name)))));

        return items.ToArray();
    }

    MenuItem[] GetShowItems()
    {
        // Get current branch, commit branch in/out and all shown branches.
        var branches =
            new[] { repo.GetCurrentBranch() }
            .Concat(repo.GetCommitBranches())
            .Concat(repo.Repo.Branches);

        return ToShowBranchesItems(branches, true);
    }


    MenuItem[] GetSwitchToItems()
    {
        var currentName = repo.CurrentBranch?.CommonName ?? "";
        var branches = repo.Repo.Branches
             .Where(b => b.CommonName != currentName)
             .DistinctBy(b => b.CommonName)
             .OrderBy(b => b.CommonName);

        return branches.Select(b =>
            Item(b.DisplayName, "", () => repo.SwitchTo(b.Name)))
            .ToArray();
    }

    private MenuItem[] GetDeleteItems()
    {
        return repo.GetAllBranches()
            .Where(b => b.IsGitBranch && !b.IsMainBranch && !b.IsCurrent && !b.IsLocalCurrent)
            .DistinctBy(b => b.CommonName)
            .OrderBy(b => b.CommonName)
            .Select(b => Item(b.DisplayName, "", () => repo.DeleteBranch(b.Name)))
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
            Item(b.DisplayName, "", () => repo.MergeBranch(b.Name)))
            .ToArray();
    }



    MenuItem[] GetHideItems()
    {
        var branches = repo.Repo.Branches
            .Where(b => !b.IsMainBranch)
            .DistinctBy(b => b.CommonName)
            .OrderBy(b => b.CommonName);

        return branches.Select(b =>
            Item(b.DisplayName, "", () => repo.HideBranch(b.Name)))
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

        items.Add(SubMenu("Recent Branches", "", ToShowBranchesItems(recentBranches)));
        items.Add(SubMenu("Live Branches", "", ToShowBranchesItems(liveBranches)));
        items.Add(SubMenu("Live and Deleted Branches", "", ToShowBranchesItems(liveAndDeletedBranches)));

        return items.ToArray();
    }

    MenuItem[] ToShowBranchesItems(IEnumerable<Branch> branches, bool canBeOutside = false)
    {
        var cic = repo.CurrentIndexCommit;
        return branches
            .DistinctBy(b => b.CommonName)
            .Select(b => Item(ToShowName(b, cic, canBeOutside), "", () => repo.ShowBranch(b.Name)))
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

        return ToShowName(branch, isBranchIn, isBranchOut);
    }

    string ToShowName(Branch branch, bool isBranchIn = false, bool isBranchOut = false)
    {
        string name = branch.DisplayName;
        name = branch.IsGitBranch ? " " + name : "~" + name;
        name = isBranchIn ? "╮" + name : name;
        name = isBranchOut ? "╯" + name : name;
        name = isBranchIn || isBranchOut ? name : " " + name;
        name = branch.IsCurrent || branch.IsLocalCurrent ? "●" + name : " " + name;

        return name;
    }

    string Sid(string id) => id == Repo.UncommittedId ? "uncommitted" : id.Substring(0, 6);
}
