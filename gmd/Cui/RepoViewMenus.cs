using gmd.Common;
using gmd.Cui.Common;
using gmd.Installation;
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
    readonly int RecentCount = 15;
    readonly IRepo repo;
    readonly IRepoCommands cmds;
    readonly IState states;
    readonly IConfig config;
    readonly IRepoConfig repoConfig;
    readonly IUpdater updater;
    private readonly IConfigDlg configDlg;

    internal RepoViewMenus(IRepo repo, IState states, IConfig config,
    IRepoConfig repoConfig, IUpdater updater, IConfigDlg configDlg)
    {
        this.repo = repo;
        this.cmds = repo.Cmd;
        this.states = states;
        this.config = config;
        this.repoConfig = repoConfig;
        this.updater = updater;
        this.configDlg = configDlg;
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

        items.Add(UI.MenuSeparator("Switch to"));
        items.AddRange(switchToItems);

        items.Add(UI.MenuSeparator("Open"));
        items.AddRange(showItems);

        items.Add(UI.MenuSeparator("More"));

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
        var branchName = repo.CurrentBranch?.DisplayName ?? "";
        var commit = repo.RowCommit;
        var sidText = Sid(repo.RowCommit.Id);
        var currentSidText = Sid(repo.GetCurrentCommit().Sid);

        if (releases.IsUpdateAvailable)
        {
            items = items.Add(UI.MenuSeparator("New Release Available !!!"),
            Item("Update to Latest ...", "", () => cmds.UpdateRelease()),
            UI.MenuSeparator());
        }
        var curcom = repo.GetCurrentCommit();
        var isAhead = repo.GetCurrentCommit().IsAhead;

        return items.Add(
            Item("Toggle Details ...", "Enter", () => cmds.ToggleDetails()),
            Item("Commit ...", "C",
                () => cmds.CommitFromMenu(false),
                () => !repo.Status.IsOk),
            Item($"Amend {currentSidText} ...", "A",
                () => cmds.CommitFromMenu(true),
                () => repo.GetCurrentCommit().IsAhead),
            SubMenu("Undo", "", GetUndoItems()),

            SubMenu("Diff", "", GetDiffItems()),
            SubMenu("Open/Show Branch", "->", GetShowBranchItems()),
            SubMenu("Hide Branch", "<-", GetHideItems()),
            SubMenu("Switch/Checkout", "", GetSwitchToItems()),
            SubMenu("Push/Publish", "", GetPushItems(), () => cmds.CanPush()),
            SubMenu("Pull/Update", "", GetPullItems(), () => cmds.CanPull()),
            SubMenu($"Merge from", "", GetMergeFromItems(), () => GetMergeFromItems().Any()),
            Item("Create Branch ...", "B", () => cmds.CreateBranch()),
            Item("Create Branch from commit ...", "",
                () => cmds.CreateBranchFromCommit(), () => repo.Status.IsOk),
            SubMenu("Delete Branch", "", GetDeleteItems()),
            SubMenu("Stash", "", GetStashMenuItems()),
            SubMenu("Tag", "", GetTagItems()),
            SubMenu("Resolve Ambiguity", "", GetAmbiguousItems(), () => GetAmbiguousItems().Any()),

            UI.MenuSeparator(""),
            SubMenu("More", "", GetMoreItems()),
            Item("Quit", "Esc", () => UI.Shutdown()));
    }

    IEnumerable<MenuItem> GetTagItems()
    {
        return EnumerableEx.From(
            Item("Add Tag ...", "", () => cmds.AddTag(), () => !repo.RowCommit.IsUncommitted),
            SubMenu("Remove Tag", "", GetDeleteTagItems(), () => GetDeleteTagItems().Any())
        );
    }

    IEnumerable<MenuItem> GetDeleteTagItems()
    {
        var commit = repo.RowCommit;
        return commit.Tags.Select(t => Item(t.Name, "", () => cmds.DeleteTag(t.Name)));
    }


    IEnumerable<MenuItem> GetDiffItems()
    {
        return EnumerableEx.From(
            Item("Commit Diff ...", "D", () => cmds.ShowCurrentRowDiff()),
            SubMenu($"Diff Branch to", "", GetPreviewMergeItems(false, false), () => GetPreviewMergeItems(false, false).Any()),
            SubMenu($"Diff {Sid(repo.RowCommit.Id)} to", "", GetPreviewMergeItems(true, false), () => GetPreviewMergeItems(true, false).Any()),
            SubMenu($"Diff Branch from", "", GetPreviewMergeItems(false, true), () => GetPreviewMergeItems(false, true).Any()),
            Item($"Diff {Sid(repo.RowCommit.Id)} from", "", () => cmds.PreviewMergeBranch(repo.RowCommit.BranchName, true, true)),
            SubMenu("Stash Diff", "", GetStashDiffItems(), () => GetStashDiffItems().Any())
        );
    }

    IEnumerable<MenuItem> GetMoreItems()
    {
        return EnumerableEx.From(
            Item("Search/Filter ...", "F", () => cmds.Filter()),
            Item("Refresh/Reload", "R", () => cmds.Refresh()),
            Item("File History ...", "", () => cmds.ShowFileHistory()),
            SubMenu("Open/Clone Repo", "", GetOpenRepoItems()),
            Item("Change Branch Color", "G", () => cmds.ChangeBranchColor(), () => !repo.Branch(repo.RowCommit.BranchName).IsMainBranch),
            Item("Help ...", "H", () => cmds.ShowHelp()),
            Item("Config ...", "", () => configDlg.Show(repo.RepoPath)),
            Item("About ...", "", () => cmds.ShowAbout())
        );
    }

    private IEnumerable<MenuItem> GetStashMenuItems()
    {
        return EnumerableEx.From(
            Item("Stash Changes", "", () => cmds.Stash(), () => !repo.Status.IsOk),
            SubMenu("Stash Pop", "", GetStashPopItems(), () => repo.Status.IsOk && GetStashPopItems().Any()),
            SubMenu("Stash Diff", "", GetStashDiffItems(), () => GetStashDiffItems().Any()),
            SubMenu("Stash Drop", "", GetStashDropItems(), () => GetStashDropItems().Any())
        );
    }

    IEnumerable<MenuItem> GetStashPopItems()
    {
        return repo.Repo.Stashes.Select(s =>
            Item($"{s.Message}", "", () => cmds.StashPop(s.Name)));
    }

    IEnumerable<MenuItem> GetStashDropItems()
    {
        return repo.Repo.Stashes.Select(s =>
            Item($"{s.Message}", "", () => cmds.StashDrop(s.Name)));
    }

    IEnumerable<MenuItem> GetStashDiffItems()
    {
        return repo.Repo.Stashes.Select(s =>
            Item($"{s.Message}", "", () => cmds.StashDiff(s.Name)));
    }
    IEnumerable<MenuItem> GetConfigItems()
    {
        var path = repo.RepoPath;
        var previewTxt = config.Get().AllowPreview ? "Disable Preview Releases" : "Enable Preview Releases";
        var metaSyncTxt = repoConfig.Get(path).SyncMetaData ? "Disable this Repo Sync Metadata" : "Enable this Repo Sync Metadata";

        return EnumerableEx.From(
             Item(previewTxt, "", () =>
             {
                 config.Set(c => c.AllowPreview = !c.AllowPreview);
                 updater.CheckUpdateAvailableAsync().RunInBackground();
             }),
             Item(metaSyncTxt, "", () => repoConfig.Set(path, c => c.SyncMetaData = !c.SyncMetaData))
        );
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

        return ToSwitchBranchesItems(branches);
    }

    IEnumerable<MenuItem> GetDeleteItems()
    {
        return repo.GetAllBranches()
            .Where(b => b.IsGitBranch && !b.IsMainBranch && !b.IsCurrent && !b.IsLocalCurrent)
            .DistinctBy(b => b.CommonName)
            .OrderBy(b => b.CommonName)
            .Select(b => Item($"{ToBranchMenuName(b)} ...", "", () => cmds.DeleteBranch(b.Name)));
    }


    IEnumerable<MenuItem> GetMergeFromItems()
    {
        if (!repo.Status.IsOk)
        {
            return Enumerable.Empty<MenuItem>();
        }

        var sidText = Sid(repo.RowCommit.Id);
        var commit = repo.RowCommit;
        var currentName = repo.CurrentBranch?.CommonName ?? "";

        // Get all branches except current
        var branches = repo.Branches
             .Where(b => b.CommonName != currentName)
             .DistinctBy(b => b.CommonName)
             .OrderBy(b => b.CommonName);

        // Include commit if not on current branch
        var commitItems = repo.Branch(commit.BranchName) != repo.CurrentBranch
            ? new[] { Item($"Commit {commit.Sid}", "", () => cmds.MergeBranch(commit.Id)) }
            : Enumerable.Empty<MenuItem>();

        // Incluce cherry pic if not on current branch
        var cherryPicItems = repo.RowCommit.Id != repo.CurrentBranch?.TipId
            ? new[] { Item($"Cherry Pic {sidText}", "", () => cmds.CherryPic(commit.Id), () => repo.Status.IsOk) }
            : Enumerable.Empty<MenuItem>();

        var items = branches
            .Select(b => Item(ToBranchMenuName(b), "", () => cmds.MergeBranch(b.Name)))
            .Concat(commitItems)
            .Concat(cherryPicItems);

        return items;
    }

    IEnumerable<MenuItem> GetPreviewMergeItems(bool isFromCurrentCommit, bool isSwitch)
    {
        var commit = repo.RowCommit;
        var currentName = repo.CurrentBranch?.CommonName ?? "";
        var branches = repo.Branches
             .Where(b => b.CommonName != currentName)
             .DistinctBy(b => b.CommonName)
             .OrderBy(b => b.CommonName);

        return branches.Select(b => Item(ToBranchMenuName(b), "", () => cmds.PreviewMergeBranch(b.Name, isFromCurrentCommit, isSwitch)));
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
            .Take(RecentCount);

        var ambiguousBranches = allBranches
            .Where(b => b.AmbiguousTipId != "")
            .OrderBy(b => b.CommonName);

        var items = EnumerableEx.From(
            SubMenu("Recent", "", ToShowHiarchicalBranchesItems(recentBranches)),
            SubMenu("All Live", "", ToShowHiarchicalBranchesItems(liveBranches)),
            SubMenu("All Live and Deleted", "", ToShowHiarchicalBranchesItems(liveAndDeletedBranches))
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

    IEnumerable<MenuItem> GetUncommittedFileItems() =>
        repo.GetUncommittedFiles().Select(f => Item(f, "", () => cmds.UndoUncommittedFile(f)));


    IEnumerable<MenuItem> ToShowHiarchicalBranchesItems(IEnumerable<Branch> branches)
    {
        if (branches.Count() <= RecentCount)
        {   // Too few branches to bother with submenus
            return ToShowBranchesItems(branches, false, false);
        }

        var cic = repo.RowCommit;

        // Group by first part of the b.commonName (if '/' exists in name)
        var groups = branches
            .GroupBy(b => b.CommonName.Split('/')[0])
            .OrderBy(g => g.Key);

        // If only one item in group, then just show branch, otherwise show submenu
        return groups.Select(g =>
            g.Count() == 1
                ? Item(ToBranchMenuName(g.First(), cic, false), "", () => cmds.ShowBranch(g.First().Name, false))
                : SubMenu($"   {g.Key}/┅", "", ToShowBranchesItems(g, false, false)));
    }

    IEnumerable<MenuItem> ToShowBranchesItems(
        IEnumerable<Branch> branches, bool canBeOutside = false, bool includeAmbiguous = false)
    {
        var cic = repo.RowCommit;
        return branches
            .DistinctBy(b => b.CommonName)
            .Select(b => Item(ToBranchMenuName(b, cic, canBeOutside), "", () => cmds.ShowBranch(b.Name, includeAmbiguous)));
    }

    IEnumerable<MenuItem> ToSwitchBranchesItems(IEnumerable<Branch> branches)
    {
        var cic = repo.RowCommit;
        return branches
            .DistinctBy(b => b.CommonName)
            .Select(b => Item(ToBranchMenuName(b, cic, false), "", () => cmds.SwitchTo(b.Name)));
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
