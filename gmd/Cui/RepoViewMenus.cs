using gmd.Common;
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
    private readonly IStates states;

    internal RepoViewMenus(IRepo repo, IStates states)
    {
        this.repo = repo;
        this.states = states;
    }

    public void ShowMainMenu()
    {
        int x = repo.ContentWidth / 2 - 10;
        var menu = new ContextMenu(x, 0, SubMenu("", "", GetMainMenuItems()));
        menu.Show();
    }

    public void ShowShowBranchesMenu()
    {
        List<MenuItem> items = new List<MenuItem>();
        var showItems = GetShowItems();
        var switchToItems = GetSwitchToItems();


        if (showItems.Any())
        {
            items.Add(UI.MenuSeparator("Show"));
            items.AddRange(showItems);
        }

        if (switchToItems.Any())
        {
            items.Add(UI.MenuSeparator("Switch to"));
            items.AddRange(switchToItems);
        }

        if (items.Any())
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

    IEnumerable<MenuItem> GetMainMenuItems()
    {
        var releases = states.Get().Releases;
        var items = EnumerableEx.From<MenuItem>();

        if (releases.IsUpdateAvailable)
        {
            items = items.Add(UI.MenuSeparator("New Release !!!"),
            Item("Update to Latest ...", "", () => repo!.UpdateRelease()),
            UI.MenuSeparator());
        }

        return items.Add(
            UI.MenuSeparator($"Commit {Sid(repo.CurrentIndexCommit.Id)}"),
            Item("Toggle Details ...", "Enter", () => repo.ToggleDetails()),
            Item("Commit ...", "C",
                () => repo.CommitFromMenu(),
                () => !repo.Repo.Status.IsOk),
            Item("Commit Diff ...", "D", () => repo.ShowCurrentRowDiff()),
            SubMenu("Undo", "", GetUndoItems()),

            UI.MenuSeparator("Branches"),
            SubMenu("Show Branch", "->", GetShowBranchItems()),
            SubMenu("Hide Branch", "<-", GetHideItems()),
            SubMenu("Switch/Checkout", "", GetSwitchToItems()),
            SubMenu("Push", "", GetPushItems(), () => repo.CanPush()),
            SubMenu("Update/Pull", "", GetPullItems(), () => repo.CanPull()),
            SubMenu("Merge", "", GetMergeItems()),
            Item("Create Branch ...", "B", () => repo.CreateBranch()),
            Item("Create Branch from commit ...", "",
                () => repo.CreateBranchFromCommit(), () => repo.Repo.Status.IsOk),
            SubMenu("Delete Branch", "", GetDeleteItems()),
            SubMenu("Resolve Ambiguity", "", GetAmbiguousItems(), () => GetAmbiguousItems().Any()),

            UI.MenuSeparator("More"),
            Item("Seach/Filter ...", "F", () => repo.Filter()),
            Item("Refresh/Reload", "R", () => repo.Refresh()),
            SubMenu("Open Repo", "", GetOpenRepoItems()),
            Item("About ...", "A", () => repo.ShowAbout()),
            Item("Quit", "Esc", () => UI.Shutdown()));
    }

    IEnumerable<MenuItem> GetAmbiguousItems()
    {
        if (!repo.CurrentIndexCommit.IsAmbiguous)
        {
            return Enumerable.Empty<MenuItem>();
        }

        var branch = repo.ViewedBranchByName(repo.CurrentIndexCommit.BranchName);
        return branch.AmbiguousBranchNames
            .Select(name => Item(repo.AllBranchByName(name).DisplayName,
                 "", () => repo.SetAsParent(branch, name)));
    }

    MenuBarItem SubMenu(string title, string key, IEnumerable<MenuItem> children, Func<bool>? canExecute = null) =>
        new MenuBarItem(title, key == "" ? "" : key + " ", null, canExecute) { Children = children.ToArray() };

    MenuItem Item(string title, string key, Action action, Func<bool>? canExecute = null) =>
        new MenuItem(title, key == "" ? "" : key + " ", action, canExecute);

    IEnumerable<MenuItem> GetPushItems()
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

        return items.DistinctBy(b => b.Title);
    }


    IEnumerable<MenuItem> GetOpenRepoItems()
    {
        return GetRecentRepoItems().Add(
            UI.MenuSeparator(),
            Item("Browse ...", "", () => repo.ShowBrowseDialog()),
            new MenuItem("Clone ...", "", () => { }, () => false));
    }


    IEnumerable<MenuItem> GetRecentRepoItems() =>
        states.Get().RecentFolders.Select(path => Item(path, "", () => repo.ShowRepo(path)));


    IEnumerable<MenuItem> GetPullItems()
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
            .Select(b => (Item($"{ToShowName(b)}", "", () => repo.PullBranch(b.Name)))));
        return items.DistinctBy(b => b.Title);
    }

    IEnumerable<MenuItem> GetShowItems()
    {
        // Get current branch, commit branch in/out and all shown branches.
        var branches =
            new[] { repo.GetCurrentBranch() }
            .Concat(repo.GetCommitBranches())
            .Concat(repo.Repo.Branches);

        return ToShowBranchesItems(branches, true);
    }


    IEnumerable<MenuItem> GetSwitchToItems()
    {
        var currentName = repo.CurrentBranch?.CommonName ?? "";
        var branches = repo.Repo.Branches
             .Where(b => b.CommonName != currentName)
             .DistinctBy(b => b.CommonName)
             .OrderBy(b => b.CommonName);

        return branches.Select(b => Item(b.DisplayName, "", () => repo.SwitchTo(b.Name)));
    }

    IEnumerable<MenuItem> GetDeleteItems()
    {
        return repo.GetAllBranches()
            .Where(b => b.IsGitBranch && !b.IsMainBranch && !b.IsCurrent && !b.IsLocalCurrent)
            .DistinctBy(b => b.CommonName)
            .OrderBy(b => b.CommonName)
            .Select(b => Item(b.DisplayName, "", () => repo.DeleteBranch(b.Name)));
    }


    IEnumerable<MenuItem> GetMergeItems()
    {
        if (repo.HasUncommittedChanges)
        {
            return Enumerable.Empty<MenuItem>();
        }

        var currentName = repo.CurrentBranch?.CommonName ?? "";
        var branches = repo.Repo.Branches
             .Where(b => b.CommonName != currentName)
             .DistinctBy(b => b.CommonName)
             .OrderBy(b => b.CommonName);

        return branches.Select(b => Item(b.DisplayName, "", () => repo.MergeBranch(b.Name)));
    }



    IEnumerable<MenuItem> GetHideItems()
    {
        var branches = repo.Repo.Branches
            .Where(b => !b.IsMainBranch)
            .DistinctBy(b => b.CommonName)
            .OrderBy(b => b.CommonName);

        return branches.Select(b =>
            Item(b.DisplayName, "", () => repo.HideBranch(b.Name)));
    }


    IEnumerable<MenuItem> GetShowBranchItems()
    {
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

        var ambiguousBranches = allBranches
            .Where(b => b.AmbiguousTipId != "")
            .OrderBy(b => b.CommonName);

        var items = EnumerableEx.From(
            SubMenu("Recent", "", ToShowBranchesItems(recentBranches)),
            SubMenu("All Live", "", ToShowBranchesItems(liveBranches)),
            SubMenu("All Live and Deleted", "", ToShowBranchesItems(liveAndDeletedBranches))
        );

        return ambiguousBranches.Any()
            ? items.Append(SubMenu("Ambiguous", "", ToShowBranchesItems(ambiguousBranches, false, true)))
            : items;
    }


    IEnumerable<MenuItem> GetUndoItems()
    {
        string id = repo.CurrentIndexCommit.Id;
        string sid = repo.CurrentIndexCommit.Sid;

        return EnumerableEx.From(
            SubMenu("Undo/Restore Uncommitted File", "", GetUncommittedFileItems(), () => repo.CanUndoUncommitted()),
            Item($"Undo Commit {sid}", "", () => repo.UndoCommit(id), () => repo.CanUndoCommit()),
            Item($"Uncommit Last Commit", "", () => repo.UncommitLastCommit(), () => repo.CanUncommitLastCommit()),
            UI.MenuSeparator(),
            Item("Undo/Restore all Uncommitted Changes", "",
                () => repo.UndoAllUncommittedChanged(), () => repo.CanUndoUncommitted()),
            Item("Clean/Restore Working Folder", "", () => repo.CleanWorkingFolder())
        );

    }

    private IEnumerable<MenuItem> GetUncommittedFileItems() =>
        repo.GetUncommittedFiles().Select(f => Item(f, "", () => repo.UndoUncommittedFile(f)));


    IEnumerable<MenuItem> ToShowBranchesItems(
        IEnumerable<Branch> branches, bool canBeOutside = false, bool includeAmbiguous = false)
    {
        var cic = repo.CurrentIndexCommit;
        return branches
            .DistinctBy(b => b.CommonName)
            .Select(b => Item(ToShowName(b, cic, canBeOutside), "", () => repo.ShowBranch(b.Name, includeAmbiguous)));
    }

    string ToShowName(Branch branch, Commit cic, bool canBeOutside)
    {
        bool isBranchIn = false;
        bool isBranchOut = false;
        if (canBeOutside && !repo.Repo.BranchByName.TryGetValue(branch.Name, out var _))
        {
            // The branch is currently not shown
            if (repo.Repo.AugmentedRepo.BranchByName.TryGetValue(branch.Name, out var b))
            {
                // The branch is not shown, but does exist
                if (cic.ParentIds.Count > 1 && cic.ParentIds[1] == b.TipId)
                {   // Is a branch in '╮' branch                     
                    isBranchIn = true;
                }
                else if (cic.ChildIds.Contains(b.BottomId))
                {   // Is branch out '╯' branch
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
