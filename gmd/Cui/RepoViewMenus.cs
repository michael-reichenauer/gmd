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
    readonly IRepoCommands cmds;
    private readonly IStates states;

    internal RepoViewMenus(IRepo repo, IStates states)
    {
        this.repo = repo;
        this.cmds = repo.Cmd;
        this.states = states;
    }

    public void ShowMainMenu()
    {
        int x = repo.ContentWidth / 2 - 10;
        var menu = new ContextMenu(x, 0, SubMenu("", "", GetMainMenuItems(x, 0)));
        menu.Show();
    }

    public void ShowShowBranchesMenu()
    {
        List<MenuItem> items = new List<MenuItem>();
        var showItems = GetShowItems();
        var scrollItems = GetScrollToItems();
        var switchToItems = GetSwitchToItems();

        if (showItems.Any())
        {
            items.Add(UI.MenuSeparator("Open"));
            items.AddRange(showItems);
        }

        if (switchToItems.Any())
        {
            items.Add(UI.MenuSeparator("Switch to"));
            items.AddRange(switchToItems);
        }

        if (scrollItems.Any())
        {
            items.Add(UI.MenuSeparator("Scroll to"));
            items.AddRange(scrollItems);
        }

        if (items.Any())
        {
            items.Add(UI.MenuSeparator("More"));
        }

        items.Add(SubMenu("Show Branch", "", GetShowBranchItems()));
        items.Add(SubMenu("Main Menu", "M", GetMainMenuItems(repo.CurrentPoint.X, repo.CurrentPoint.Y)));

        var menu = new ContextMenu(repo.CurrentPoint.X, repo.CurrentPoint.Y, new MenuBarItem(items.ToArray()));
        menu.Show();
    }

    public void ShowHideBranchesMenu()
    {
        List<MenuItem> items = new List<MenuItem>();

        items.Add(UI.MenuSeparator("Close/Hide"));
        items.AddRange(GetHideItems());

        var menu = new ContextMenu(repo.CurrentPoint.X, repo.CurrentPoint.Y, new MenuBarItem(items.ToArray()));
        menu.Show();
    }

    IEnumerable<MenuItem> GetMainMenuItems(int x, int y)
    {
        var releases = states.Get().Releases;
        var items = EnumerableEx.From<MenuItem>();

        if (releases.IsUpdateAvailable)
        {
            items = items.Add(UI.MenuSeparator("New Release !!!"),
            Item("Update to Latest ...", "", () => cmds.UpdateRelease()),
            UI.MenuSeparator());
        }

        return items.Add(
            UI.MenuSeparator($"Commit {Sid(repo.RowCommit.Id)}"),
            Item("Toggle Details ...", "Enter", () => cmds.ToggleDetails()),
            Item("Commit ...", "C",
                () => cmds.CommitFromMenu(),
                () => !repo.Status.IsOk),
            Item("Commit Diff ...", "D", () => cmds.ShowCurrentRowDiff()),
            SubMenu("Undo", "", GetUndoItems()),

            UI.MenuSeparator("Branches"),
            SubMenu("Open/Show Branch", "->", GetShowBranchItems()),
            SubMenu("Close/Hide Branch", "<-", GetHideItems()),
            SubMenu("Switch/Checkout", "", GetSwitchToItems()),
            SubMenu("Push", "", GetPushItems(), () => cmds.CanPush()),
            SubMenu("Update/Pull", "", GetPullItems(), () => cmds.CanPull()),
            SubMenu("Merge", "", GetMergeItems()),
            Item("Create Branch ...", "B", () => cmds.CreateBranch()),
            Item("Create Branch from commit ...", "",
                () => cmds.CreateBranchFromCommit(), () => repo.Status.IsOk),
            SubMenu("Delete Branch", "", GetDeleteItems()),
            SubMenu("Resolve Ambiguity", "", GetAmbiguousItems(), () => GetAmbiguousItems().Any()),

            UI.MenuSeparator("More"),
            Item("Seach/Filter ...", "F", () => cmds.Filter()),
            Item("Refresh/Reload", "R", () => cmds.Refresh()),
            Item("File History ...", "", () => cmds.ShowFileHistory()),
            SubMenu("Open/Clone Repo", "", GetOpenRepoItems()),
            Item("About ...", "A", () => cmds.ShowAbout()),
            Item("Quit", "Esc", () => UI.Shutdown()));
    }



    IEnumerable<MenuItem> GetAmbiguousItems()
    {
        var items = Enumerable.Empty<MenuItem>();
        var commit = repo.RowCommit;
        if (!commit.IsAmbiguous && !commit.IsBranchSetByUser)
        {
            return items;
        }

        if (commit.IsBranchSetByUser)
        {
            items = items.Append(Item("Undo Resolved Ambiguity", "", () => cmds.UnresolveAmbiguity(commit.Id)));
        }

        var branch = repo.Branch(commit.BranchName);
        return items
            .Concat(branch.AmbiguousBranchNames.Select(n => repo.AllBranchByName(n))
                .DistinctBy(b => b.CommonName)
                .Select(b => Item(ToBranchMenuName(b), "", () => cmds.ResolveAmbiguity(branch, b.DisplayName))));
    }

    MenuBarItem SubMenu(string title, string key, IEnumerable<MenuItem> children, Func<bool>? canExecute = null) =>
        new MenuBarItem(title, key == "" ? "" : key + " ", null, canExecute) { Children = children.ToArray() };

    MenuItem Item(string title, string key, Action action, Func<bool>? canExecute = null) =>
        new MenuItem(title, key == "" ? "" : key + " ", action, canExecute);

    IEnumerable<MenuItem> GetPushItems()
    {
        List<MenuItem> items = new List<MenuItem>();

        items.Add(Item("Push All Branches", "P", () => cmds.PushAllBranches()));

        if (repo.CurrentBranch != null)
        {
            items.Add(Item(ToBranchMenuName(repo.CurrentBranch), "",
                    () => cmds.PushCurrentBranch(),
                    () => cmds.CanPushCurrentBranch()));
        }
        items.AddRange(repo.Branches
            .Where(b => !b.IsRemote && !b.IsCurrent && b.HasLocalOnly && !b.HasRemoteOnly)
            .Select(b => (Item($"Push {ToBranchMenuName(b)}", "", () => cmds.PushBranch(b.Name)))));

        return items.DistinctBy(b => b.Title);
    }


    IEnumerable<MenuItem> GetOpenRepoItems()
    {
        return GetRecentRepoItems().Add(
            UI.MenuSeparator(),
            Item("Browse ...", "", () => cmds.ShowBrowseDialog()),
            Item("Clone ...", "", () => cmds.Clone(), () => true));
    }


    IEnumerable<MenuItem> GetRecentRepoItems() =>
        states.Get().RecentFolders.Where(Files.DirExists).Select(path => Item(path, "", () => cmds.ShowRepo(path)));


    IEnumerable<MenuItem> GetPullItems()
    {
        List<MenuItem> items = new List<MenuItem>();
        items.Add(Item("Update/Pull All Branches", "U", () => cmds.PullAllBranches()));
        if (repo.CurrentBranch != null)
        {
            items.Add(Item(ToBranchMenuName(repo.CurrentBranch), "",
                    () => cmds.PullCurrentBranch(),
                    () => cmds.CanPullCurrentBranch()));
        }
        items.AddRange(repo.Branches
            .Where(b => b.IsRemote && !b.IsCurrent && b.HasRemoteOnly)
            .Select(b => (Item($"{ToBranchMenuName(b)}", "", () => cmds.PullBranch(b.Name)))));
        return items.DistinctBy(b => b.Title);
    }

    IEnumerable<MenuItem> GetShowItems()
    {
        // Get current branch, commit branch in/out and all shown branches
        var shownBranches = repo.Branches;
        var branches =
            new[] { repo.GetCurrentBranch() }
            .Concat(repo.GetCommitBranches())
            .Concat(repo.Branches)
            .Where(b => !repo.Branches.ContainsBy(bb => bb.CommonName == b.CommonName));

        return ToShowBranchesItems(branches, true);
    }

    IEnumerable<MenuItem> GetScrollToItems()
    {
        // Get current branch, commit branch in/out and all shown branches
        var branches =
            new[] { repo.GetCurrentBranch() }
            .Concat(repo.GetCommitBranches())
            .Concat(repo.Branches)
            .Where(b => repo.Branches.Contains(b));

        return ToShowBranchesItems(branches, true);
    }


    IEnumerable<MenuItem> GetSwitchToItems()
    {
        var currentName = repo.CurrentBranch?.CommonName ?? "";
        var branches = repo.Branches
             .Where(b => b.CommonName != currentName)
             .DistinctBy(b => b.CommonName)
             .OrderBy(b => b.CommonName);

        return branches.Select(b => Item(b.DisplayName, "", () => cmds.SwitchTo(b.Name)));
    }

    IEnumerable<MenuItem> GetDeleteItems()
    {
        return repo.GetAllBranches()
            .Where(b => b.IsGitBranch && !b.IsMainBranch && !b.IsCurrent && !b.IsLocalCurrent)
            .DistinctBy(b => b.CommonName)
            .OrderBy(b => b.CommonName)
            .Select(b => Item(ToBranchMenuName(b), "", () => cmds.DeleteBranch(b.Name)));
    }


    IEnumerable<MenuItem> GetMergeItems()
    {
        if (!repo.Status.IsOk)
        {
            return Enumerable.Empty<MenuItem>();
        }

        var currentName = repo.CurrentBranch?.CommonName ?? "";
        var branches = repo.Branches
             .Where(b => b.CommonName != currentName)
             .DistinctBy(b => b.CommonName)
             .OrderBy(b => b.CommonName);

        return branches.Select(b => Item(ToBranchMenuName(b), "", () => cmds.MergeBranch(b.Name)));
    }

    IEnumerable<MenuItem> GetHideItems()
    {
        var mainBranch = repo.Branches.First(b => b.IsMainBranch);
        var branches = repo.Branches
            .Where(b => !b.IsMainBranch && b.CommonName != mainBranch.CommonName)
            .DistinctBy(b => b.CommonName)
            .OrderBy(b => b.CommonName);

        return branches.Select(b =>
            Item(ToBranchMenuName(b), "", () => cmds.HideBranch(b.Name)));
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
            .OrderBy(b => repo.Repo.AugmentedRepo.CommitById[b.TipId].GitIndex)
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
        string id = repo.RowCommit.Id;
        string sid = repo.RowCommit.Sid;

        return EnumerableEx.From(
            SubMenu("Undo/Restore Uncommitted File", "", GetUncommittedFileItems(), () => cmds.CanUndoUncommitted()),
            Item($"Undo Commit {sid}", "", () => cmds.UndoCommit(id), () => cmds.CanUndoCommit()),
            Item($"Uncommit Last Commit", "", () => cmds.UncommitLastCommit(), () => cmds.CanUncommitLastCommit()),
            UI.MenuSeparator(),
            Item("Undo/Restore all Uncommitted Changes", "",
                () => cmds.UndoAllUncommittedChanged(), () => cmds.CanUndoUncommitted()),
            Item("Clean/Restore Working Folder", "", () => cmds.CleanWorkingFolder())
        );

    }

    private IEnumerable<MenuItem> GetUncommittedFileItems() =>
        repo.GetUncommittedFiles().Select(f => Item(f, "", () => cmds.UndoUncommittedFile(f)));


    IEnumerable<MenuItem> ToShowBranchesItems(
        IEnumerable<Branch> branches, bool canBeOutside = false, bool includeAmbiguous = false)
    {
        var cic = repo.RowCommit;
        return branches
            .DistinctBy(b => b.CommonName)
            .Select(b => Item(ToBranchMenuName(b, cic, canBeOutside), "", () => cmds.ShowBranch(b.Name, includeAmbiguous)));
    }

    string ToBranchMenuName(Branch branch, Commit cic, bool canBeOutside)
    {
        bool isBranchIn = false;
        bool isBranchOut = false;
        if (canBeOutside && !repo.Repo.BranchByName.TryGetValue(branch.Name, out var _))
        {   // The branch is currently not shown
            if (repo.Repo.AugmentedRepo.BranchByName.TryGetValue(branch.Name, out var b))
            {
                // The branch is not shown, but does exist
                if (cic.ParentIds.Count > 1 &&
                    repo.Repo.AugmentedRepo.CommitById[cic.ParentIds[1]].BranchName == b.Name)
                {   // Is a branch merge in '╮' branch                     
                    isBranchIn = true;
                }
                else if (cic.ChildIds.ContainsBy(id =>
                     repo.Repo.AugmentedRepo.CommitById[id].BranchName == b.Name))
                {   // Is branch out '╯' branch
                    isBranchOut = true;
                }
            }
        }

        return ToBranchMenuName(branch, isBranchIn, isBranchOut);
    }

    string ToBranchMenuName(Branch branch, bool isBranchIn = false, bool isBranchOut = false)
    {
        string name = branch.CommonName;
        name = branch.IsGitBranch ? " " + branch.DisplayName : "~" + name;
        name = isBranchIn ? "╮" + name : name;
        name = isBranchOut ? "╯" + name : name;
        name = isBranchIn || isBranchOut ? name : " " + name;
        name = branch.IsCurrent || branch.IsLocalCurrent ? "●" + name : " " + name;

        return name.Replace('_', '-');
    }

    string Sid(string id) => id == Repo.UncommittedId ? "uncommitted" : id.Substring(0, 6);
}
