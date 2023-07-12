using gmd.Common;
using gmd.Cui.Common;
using gmd.Installation;
using gmd.Server;

namespace gmd.Cui;


interface IRepoViewMenus
{
    void ShowMainMenu();
    void ShowShowBranchesMenu();
    void ShowHideBranchesMenu();
    void ShowOpenMenu();
}

class RepoViewMenus : IRepoViewMenus
{
    const int RecentCount = 15;
    const int MaxItemCount = 20;

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
        Menu.Show(-1, 0, GetMainMenuItems(), "Main Menu");
    }


    public void ShowShowBranchesMenu()
    {
        Menu.Show(repo.CurrentPoint.X, repo.CurrentPoint.Y + 1, Menu.Items
            .Items(GetSwitchToItems())
            .Separator("Show/Open")
            .Items(GetShowItems())
            .Separator()
            .SubMenu("Show Branch", "", GetShowBranchItems())
            .SubMenu("Main Menu", "M", GetMainMenuItems()),
            "Switch to");
    }

    public void ShowHideBranchesMenu()
    {
        Menu.Show(repo.CurrentPoint.X, repo.CurrentPoint.Y + 1, GetHideItems(), "Close/Hide");
    }

    public void ShowOpenMenu()
    {
        Menu.Show(-1, 0, GetOpenRepoItems(), "Open Repo");
    }

    IEnumerable<MenuItem> GetMainMenuItems()
    {
        var releases = states.Get().Releases;
        var items = Menu.Items;
        var branchName = repo.CurrentBranch?.NiceNameUnique ?? "";
        var commit = repo.RowCommit;
        var sidText = Sid(repo.RowCommit.Id);
        var currentSidText = Sid(repo.GetCurrentCommit().Sid);
        var curcom = repo.GetCurrentCommit();
        var isAhead = repo.GetCurrentCommit().IsAhead;

        if (releases.IsUpdateAvailable && !Build.IsDevInstance())
        {
            items.Separator("New Release Available !!!")
                .Item("Update to Latest ...", "", () => cmds.UpdateRelease())
                .Separator();
        }

        return items
            .SubMenu("Commit", "", GetCommitItems())
            .SubMenu("Undo", "", GetUndoItems())
            .SubMenu("Diff", "", GetDiffItems())
            .SubMenu("Show/Open Branch", "→", GetShowBranchItems())
            .SubMenu("Hide Branch", "←", GetHideItems())
            .SubMenu("Switch/Checkout", "→", GetSwitchToItems()
                .Append(Menu.Item($"Switch to Commit {Sid(repo.RowCommit.Id)}", "", () => cmds.SwitchToCommit(), () => repo.Status.IsOk && repo.RowCommit.Id != repo.GetCurrentCommit().Id)))
            .SubMenu("Push/Publish", "", GetPushItems(), () => cmds.CanPush())
            .SubMenu("Pull/Update", "", GetPullItems(), () => cmds.CanPull())
            .SubMenu($"Merge", "", GetMergeFromItems())
            .SubMenu("Create Branch", "", GetCreateBranchItems())
            .SubMenu("Delete Branch", "", GetDeleteItems())
            .SubMenu("Stash", "", GetStashMenuItems())
            .SubMenu("Tag", "", GetTagItems())
            .SubMenu("Branch Structure", "", GetBranchStructureItems())
            .Item("Search/Filter ...", "F", () => cmds.Filter())
            .Item("Refresh/Reload", "R", () => cmds.RefreshAndFetch())
            .SubMenu("Open/Clone Repo", "O", GetOpenRepoItems())
            .Item("Config ...", "", () => configDlg.Show(repo.RepoPath))
            .Item("Help ...", "H", () => cmds.ShowHelp())
            .Item("About ...", "", () => cmds.ShowAbout())
            .Item("Quit", "Esc", () => UI.Shutdown());
    }


    IEnumerable<MenuItem> GetBranchStructureItems()
    {
        var ambiguousBranches = repo.GetAllBranches()
            .Where(b => b.AmbiguousTipId != "")
            .OrderBy(b => b.NiceNameUnique)
            .Select(b => Menu.Item(ToBranchMenuName(b), "", () => cmds.ShowBranch(b.Name, true)));

        return Menu.Items
           .Item("Change Branch Color", "G", () => cmds.ChangeBranchColor(), () => !repo.Branch(repo.RowCommit.BranchName).IsMainBranch)
           .SubMenu("Move Branch left/right", "", GetMoveBranchItems())
           //.SubMenu("Resolve Ambiguity", "", GetAmbiguousItems())
           .SubMenu("Show Ambiguous Branches", "", ambiguousBranches)
           .Item("Set Branch Nanually ...", "", () => cmds.SetBranchManuallyAsync())
           .Item("Undo Set Branch", "", () => cmds.UndoSetBranch(repo.RowCommit.Id), () => repo.RowCommit.IsBranchSetByUser);
    }

    IEnumerable<MenuItem> GetCreateBranchItems() => Menu.Items
        .Item("Create Branch ...", "B", () => cmds.CreateBranch())
        .Item("Create Branch from commit ...", "", () => cmds.CreateBranchFromCommit(), () => repo.Status.IsOk);

    IEnumerable<MenuItem> GetCommitItems() => Menu.Items
        .Item("Commit ...", "C",
            () => cmds.CommitFromMenu(false),
            () => !repo.Status.IsOk)
        .Item($"Amend {Sid(repo.GetCurrentCommit().Sid)} ...", "A",
            () => cmds.CommitFromMenu(true),
            () => repo.GetCurrentCommit().IsAhead)
        .Item("Toggle Commit Details ...", "Enter", () => cmds.ToggleDetails());


    IEnumerable<MenuItem> GetTagItems() => Menu.Items
        .Item("Add Tag ...", "", () => cmds.AddTag(), () => !repo.RowCommit.IsUncommitted)
        .SubMenu("Remove Tag", "", GetDeleteTagItems());


    IEnumerable<MenuItem> GetDeleteTagItems()
    {
        var commit = repo.RowCommit;
        return commit.Tags.Select(t => Menu.Item(t.Name, "", () => cmds.DeleteTag(t.Name)));
    }


    IEnumerable<MenuItem> GetDiffItems()
    {
        return Menu.Items
            .Item("Commit Diff ...", "D", () => cmds.ShowCurrentRowDiff())
            .Item("File History ...", "", () => cmds.ShowFileHistory())
            .SubMenu($"Diff Branch to", "", GetPreviewMergeItems(false, false))
            .SubMenu($"Diff {Sid(repo.RowCommit.Id)} to", "", GetPreviewMergeItems(true, false))
            .SubMenu($"Diff Branch from", "", GetPreviewMergeItems(false, true))
            .Item($"Diff {Sid(repo.RowCommit.Id)} from", "", () => cmds.DiffWithOtherBranch(repo.RowCommit.BranchName, true, true), () => repo.Status.IsOk)
            .SubMenu("Stash Diff", "", GetStashDiffItems());
    }


    IEnumerable<MenuItem> GetMoveBranchItems()
    {
        var items = Menu.Items;

        // Get possible local, remote, pull merge branches of the row branch
        var rowHeadName = repo.RowBranch.PrimaryName;
        var rowBranches = repo.Branches.Where(b => b.PrimaryName == rowHeadName);

        // Get all branches that overlap with any of the row branches
        var overlappingBranches = rowBranches
            .SelectMany(b => repo.Graph.GetOverlappinBranches(b.Name))
            .Distinct()
            .ToList();

        if (!overlappingBranches.Any()) return items;

        // Sort on left to right shown order
        Sorter.Sort(overlappingBranches, (b1, b2) => b1.X < b2.X ? -1 : b1.X > b2.X ? 1 : 0);

        // Find possible branch on left side to move to before (skip if ancestor)
        Branch? leftBranch = null;
        for (int i = 0; i < overlappingBranches.Count; i++)
        {
            var b = overlappingBranches[i];
            if (b.B.PrimaryName == rowHeadName) break;
            leftBranch = b.B;
        }
        var leftHeadName = leftBranch != null && !IsAncestor(leftBranch, repo.RowBranch) ? leftBranch.PrimaryName : "";

        // Find possible branch on right side to move to after (skip if ancestor)
        Branch? rightBranch = null;
        for (int i = overlappingBranches.Count - 1; i >= 0; i--)
        {
            var b = overlappingBranches[i];
            if (b.B.PrimaryName == rowHeadName) break;
            rightBranch = b.B;
        }
        var rightHeadName = rightBranch != null && !IsAncestor(repo.RowBranch, rightBranch) ? rightBranch.PrimaryName : "";

        // Add menu items if movable branches found
        if (leftHeadName != "")
        {
            items.Item($"<= (Move {repo.RowBranch.NiceNameUnique} left of {leftBranch!.NiceNameUnique})", "",
                () => cmds.MoveBranch(repo.RowBranch.PrimaryName, leftHeadName, -1));
        }
        if (rightHeadName != "")
        {
            items.Item($"=> (Move {repo.RowBranch.NiceNameUnique} right of {rightBranch!.NiceNameUnique})", "",
                () => cmds.MoveBranch(repo.RowBranch.PrimaryName, rightHeadName, +1));
        }

        return items;
    }


    IEnumerable<MenuItem> GetStashMenuItems() => Menu.Items
        .Item("Stash Changes", "", () => cmds.Stash(), () => !repo.Status.IsOk)
        .SubMenu("Stash Pop", "", GetStashPopItems(), () => repo.Status.IsOk)
        .SubMenu("Stash Diff", "", GetStashDiffItems())
        .SubMenu("Stash Drop", "", GetStashDropItems());


    IEnumerable<MenuItem> GetStashPopItems() => repo.Repo.Stashes.Select(s =>
        Menu.Item($"{s.Message}", "", () => cmds.StashPop(s.Name)));

    IEnumerable<MenuItem> GetStashDropItems() => repo.Repo.Stashes.Select(s =>
        Menu.Item($"{s.Message}", "", () => cmds.StashDrop(s.Name)));

    IEnumerable<MenuItem> GetStashDiffItems() => repo.Repo.Stashes.Select(s =>
        Menu.Item($"{s.Message}", "", () => cmds.StashDiff(s.Name)));

    IEnumerable<MenuItem> GetConfigItems()
    {
        var path = repo.RepoPath;
        var previewTxt = config.Get().AllowPreview ? "Disable Preview Releases" : "Enable Preview Releases";
        var metaSyncTxt = repoConfig.Get(path).SyncMetaData ? "Disable this Repo Sync Metadata" : "Enable this Repo Sync Metadata";

        return Menu.Items
             .Item(previewTxt, "", () =>
             {
                 config.Set(c => c.AllowPreview = !c.AllowPreview);
                 updater.CheckUpdateAvailableAsync().RunInBackground();
             })
             .Item(metaSyncTxt, "", () => repoConfig.Set(path, c => c.SyncMetaData = !c.SyncMetaData));
    }

    IEnumerable<MenuItem> GetAmbiguousItems()
    {
        var items = Menu.Items;
        var commit = repo.RowCommit;
        if (!commit.IsAmbiguous && !commit.IsBranchSetByUser)
        {
            return items;
        }

        if (commit.IsBranchSetByUser)
        {
            items.Item("Undo Set Branch", "", () => cmds.UndoSetBranch(commit.Id));
        }

        var branch = repo.Branch(commit.BranchName);
        return items
            .Concat(branch.AmbiguousBranchNames.Select(n => repo.AllBranchByName(n))
                .DistinctBy(b => b.PrimaryName)
                .Select(b => Menu.Item(ToBranchMenuName(b), "", () => cmds.ResolveAmbiguity(branch, b.NiceName))));
    }


    IEnumerable<MenuItem> GetPushItems()
    {
        var items = Menu.Items
            .Item("Push All Branches", "P", () => cmds.PushAllBranches());

        if (repo.CurrentBranch != null)
        {
            items.Item(ToBranchMenuName(repo.CurrentBranch), "p",
                    () => cmds.PushCurrentBranch(),
                    () => cmds.CanPushCurrentBranch());
        }
        items.Items(repo.Branches
            .Where(b => !b.IsRemote && !b.IsCurrent && b.HasLocalOnly && !b.HasRemoteOnly)
            .Select(b => (Menu.Item($"Push {ToBranchMenuName(b)}", "", () => cmds.PushBranch(b.Name)))));

        if (repo.CurrentBranch != null && repo.CurrentBranch.RemoteName == "")
        {
            items.Item($"Publish {repo.CurrentBranch.NiceNameUnique}", "",
            () => cmds.PublishCurrentBranch());
        }

        return items.DistinctBy(b => b.Title);
    }


    IEnumerable<MenuItem> GetOpenRepoItems() => Menu.Items
        .Items(GetRecentRepoItems())
        .Separator()
        .Item("Browse ...", "", () => cmds.ShowBrowseDialog())
        .Item("Clone ...", "", () => cmds.Clone(), () => true);


    IEnumerable<MenuItem> GetRecentRepoItems() =>
        states.Get().RecentFolders
            .Where(Files.DirExists)
            .Take(10)
            .Select(path => Menu.Item(path, "", () => cmds.ShowRepo(path), () => path != repo.RepoPath));


    IEnumerable<MenuItem> GetPullItems()
    {
        var items = Menu.Items;
        items.Item("Update/Pull All Branches", "U", () => cmds.PullAllBranches());
        if (repo.CurrentBranch != null)
        {
            items.Item(ToBranchMenuName(repo.CurrentBranch), "u",
                    () => cmds.PullCurrentBranch(),
                    () => cmds.CanPullCurrentBranch());
        }
        items.Items(repo.Branches
            .Where(b => b.IsRemote && !b.IsCurrent && b.HasRemoteOnly)
            .Select(b => (Menu.Item($"{ToBranchMenuName(b)}", "", () => cmds.PullBranch(b.Name)))));
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
            .Where(b => !repo.Branches.ContainsBy(bb => bb.PrimaryName == b.PrimaryName));

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
        var currentName = repo.CurrentBranch?.PrimaryName ?? "";
        var branches = repo.Branches
             .Where(b => b.PrimaryName != currentName && b.LocalName == "" && b.PullMergeParentBranchName == "")
             .OrderBy(b => b.PrimaryName);

        return ToSwitchHiarchicalBranchesItems(branches);
    }

    IEnumerable<MenuItem> GetDeleteItems()
    {
        var branches = repo.GetAllBranches()
            .Where(b => b.IsGitBranch && !b.IsMainBranch && !b.IsCurrent && !b.IsLocalCurrent
                && b.LocalName == "" && b.PullMergeParentBranchName == "")
            .OrderBy(b => repo.Branches.ContainsBy(bb => bb.PrimaryName == b.PrimaryName) ? 0 : 1)
            .ThenBy(b => b.PrimaryName);

        return ToDeleteHiarchicalBranchesItems(branches);
    }


    IEnumerable<MenuItem> ToDeleteItems(IEnumerable<Branch> branches) => branches.Select(b =>
            Menu.Item($"{ToBranchMenuName(b)} ...", "", () => cmds.DeleteBranch(b.Name)));


    IEnumerable<MenuItem> ToDeleteHiarchicalBranchesItems(IEnumerable<Branch> branches)
    {
        if (branches.Count() <= MaxItemCount)
        {   // Too few branches to bother with submenus
            return ToDeleteItems(branches);
        }

        var cic = repo.RowCommit;

        // Group by first part of the b.commonName (if '/' or '(' exists in name)
        var groups = branches
            .GroupBy(b => b.NiceNameUnique.Split('/', '(')[0])
            .OrderBy(g => g.Key)
            .OrderBy(g => g.Count() > 1 ? 0 : 1);  // Sort groups first;

        // If only one item in group, then just show branch, otherwise show submenu
        return groups.Select(g =>
            g.Count() == 1
                ? Menu.Item($"{ToBranchMenuName(g.First())} ...", "", () => cmds.DeleteBranch(g.First().Name))
                : Menu.SubMenu($"    {g.Key}/┅", "", ToDeleteItems(g)));
    }

    IEnumerable<MenuItem> GetMergeFromItems()
    {
        if (!repo.Status.IsOk)
        {
            return Menu.Items;
        }

        var sidText = Sid(repo.RowCommit.Id);
        var commit = repo.RowCommit;
        var currentName = repo.CurrentBranch?.PrimaryName ?? "";

        // Get all branches except current
        var branches = repo.Branches
             .Where(b => b.PrimaryName != currentName && b.LocalName == "" && b.PullMergeParentBranchName == "")
             .DistinctBy(b => b.TipId)
             .OrderBy(b => b.PrimaryName);

        // Include commit if not on current branch
        var commitItems = repo.Branch(commit.BranchName) != repo.CurrentBranch
            ? Menu.Items.Item($"From Commit {commit.Sid}", "", () => cmds.MergeBranch(commit.Id))
            : Menu.Items;

        // Incluce cherry pic if not on current branch
        var cherryPicItems = repo.RowCommit.Id != repo.CurrentBranch?.TipId
            ? Menu.Items.Item($"Cherry Pic {sidText}", "", () => cmds.CherryPic(commit.Id), () => repo.Status.IsOk)
            : Menu.Items;

        var items = branches
            .Select(b => Menu.Item(ToBranchMenuName(b), "", () => cmds.MergeBranch(b.Name)))
            .Concat(commitItems)
            .Concat(cherryPicItems);

        return items;
    }

    IEnumerable<MenuItem> GetPreviewMergeItems(bool isFromCurrentCommit, bool isSwitch)
    {
        if (!repo.Status.IsOk) return Menu.Items;

        var commit = repo.RowCommit;
        var currentPrimaryName = repo.CurrentBranch?.PrimaryName ?? "";
        var branches = repo.Branches
             .Where(b => b.IsPrimary && b.PrimaryName != currentPrimaryName)
             .DistinctBy(b => b.NiceNameUnique)
             .OrderBy(b => b.NiceNameUnique);

        return branches.Select(b => Menu.Item(ToBranchMenuName(b, false, false, false), "", () => cmds.DiffWithOtherBranch(b.Name, isFromCurrentCommit, isSwitch)));
    }

    IEnumerable<MenuItem> GetHideItems()
    {
        var mainBranch = repo.Branches.First(b => b.IsMainBranch);
        var branches = repo.Branches
            .Where(b => !b.IsMainBranch && !b.IsDetached && b.PrimaryName != mainBranch.PrimaryName &&
                b.RemoteName == "" && b.PullMergeParentBranchName == "")
            .DistinctBy(b => b.NiceNameUnique)
            .OrderBy(b => b.NiceNameUnique);

        var items = ToHideHiarchicalBranchesItems(branches);
        if (repo.Branches.Count > 10)
        {
            items = items.Append(Menu.Item("Hide All", "", () => cmds.HideBranch("", true)));
        }

        return items;
    }


    IEnumerable<MenuItem> GetShowBranchItems()
    {
        var allBranches = repo.GetAllBranches();

        var liveBranches = allBranches
            .Where(b => b.IsGitBranch && b.IsPrimary)
            .OrderBy(b => b.NiceNameUnique);

        var liveAndDeletedBranches = allBranches
            .Where(b => b.IsPrimary)
            .OrderBy(b => b.NiceNameUnique)
            .ToList();

        var recentBranches = liveAndDeletedBranches
            .Where(b => b.IsPrimary)
            .OrderBy(b => repo.Repo.AugmentedRepo.CommitById[b.TipId].GitIndex)
            .Take(RecentCount);

        var ambiguousBranches = allBranches
            .Where(b => b.AmbiguousTipId != "")
            .OrderBy(b => b.NiceNameUnique);

        var items = Menu.Items
            .SubMenu("Recent", "", ToShowHiarchicalBranchesItems(recentBranches))
            .SubMenu("Active", "", ToShowHiarchicalBranchesItems(liveBranches))
            .SubMenu("Active and Deleted", "", ToShowHiarchicalBranchesItems(liveAndDeletedBranches))
            .Item("Show All", "", () => cmds.ShowBranch("", false, true));

        return ambiguousBranches.Any()
            ? items.SubMenu("Ambiguous", "", ToShowBranchesItems(ambiguousBranches, false, true))
            : items;
    }


    IEnumerable<MenuItem> GetUndoItems()
    {
        string id = repo.RowCommit.Id;
        string sid = repo.RowCommit.Sid;
        var binaryPaths = repo.Status.AddedFiles.Concat(repo.Status.ModifiedFiles)
                .Where(f => !Files.IsText(Path.Join(repo.RepoPath, f)))
                .ToList();

        return Menu.Items
            .SubMenu("Undo/Restore Uncommitted File", "", GetUncommittedFileItems(), () => cmds.CanUndoUncommitted())
            .Item($"Undo Commit {sid}", "", () => cmds.UndoCommit(id), () => cmds.CanUndoCommit())
            .Item($"Uncommit Last Commit", "", () => cmds.UncommitLastCommit(), () => cmds.CanUncommitLastCommit())
            .Separator()
            .Item("Undo/Restore all Uncommitted Binary Files", "", () => cmds.UndoUncommittedFiles(binaryPaths), () => binaryPaths.Any())
            .Item("Undo/Restore all Uncommitted Changes", "",
                () => cmds.UndoAllUncommittedChanged(), () => cmds.CanUndoUncommitted())
            .Item("Clean/Restore Working Folder", "", () => cmds.CleanWorkingFolder());
    }


    IEnumerable<MenuItem> GetUncommittedFileItems() =>
        repo.GetUncommittedFiles().Select(f => Menu.Item(f, "", () => cmds.UndoUncommittedFile(f)));

    IEnumerable<MenuItem> ToHideBranchesItems(IEnumerable<Branch> branches) => branches.Select(b =>
        Menu.Item(ToBranchMenuName(b, false, false, false), "", () => cmds.HideBranch(b.Name)));


    IEnumerable<MenuItem> ToSwitchBranchesItems(IEnumerable<Branch> branches) =>
        branches.Select(b => Menu.Item(ToBranchMenuName(b, repo.RowCommit, false, false), "", () => cmds.SwitchTo(b.Name)));

    IEnumerable<MenuItem> ToSwitchHiarchicalBranchesItems(IEnumerable<Branch> branches)
    {
        if (branches.Count() <= MaxItemCount)
        {   // Too few branches to bother with submenus
            return ToSwitchBranchesItems(branches);
        }

        // Group by first part of the b.commonName (if '/' exists in name)
        var groups = branches
            .GroupBy(b => b.PrimaryName.TrimPrefix("origin/").Split('/')[0])
            .OrderBy(g => g.Key)
            .OrderBy(g => g.Count() > 1 ? 0 : 1);  // Sort groups first;

        // If only one item in group, then just show branch, otherwise show submenu
        return groups.Select(g =>
            g.Count() == 1
                ? Menu.Item(ToBranchMenuName(g.First(), repo.RowCommit, false), "", () => cmds.SwitchTo(g.First().Name))
                : Menu.SubMenu($"    {g.Key}/┅", "", ToSwitchBranchesItems(g)))
            .ToList();
    }


    IEnumerable<MenuItem> ToHideHiarchicalBranchesItems(IEnumerable<Branch> branches)
    {
        if (branches.Count() <= MaxItemCount)
        {   // Too few branches to bother with submenus
            return ToHideBranchesItems(branches);
        }

        // Group by first part of the b.commonName (if '/' exists in name)
        var groups = branches
            .GroupBy(b => b.PrimaryName.TrimPrefix("origin/").Split('/')[0])
            .OrderBy(g => g.Key)
            .OrderBy(g => g.Count() > 1 ? 0 : 1);  // Sort groups first;

        // If only one item in group, then just show branch, otherwise show submenu
        return groups.Select(g =>
            g.Count() == 1
                ? Menu.Item(ToBranchMenuName(g.First(), repo.RowCommit, false), "", () => cmds.HideBranch(g.First().Name))
                : Menu.SubMenu($"    {g.Key}/┅", "", ToHideBranchesItems(g)))
            .ToList();
    }


    IEnumerable<MenuItem> ToShowHiarchicalBranchesItems(IEnumerable<Branch> branches)
    {
        if (branches.Count() <= MaxItemCount)
        {   // Too few branches to bother with submenus
            return ToShowBranchesItems(branches, false, false);
        }

        // Group by first part of the b.commonName (if '/' exists in name)
        var groups = branches
            .GroupBy(b => b.NiceNameUnique.Split('/', '(')[0])
            .OrderBy(g => g.Key)
            .OrderBy(g => g.Count() > 1 ? 0 : 1);  // Sort groups first;

        // If only one item in group, then just show branch, otherwise show submenu
        return groups.Select(g =>
            g.Count() == 1
                ? Menu.Item(ToBranchMenuName(g.First(), repo.RowCommit, false), "", () => cmds.ShowBranch(g.First().Name, false))
                : Menu.SubMenu($"    {g.Key}/┅", "", ToShowBranchesItems(g, false, false)));
    }


    IEnumerable<MenuItem> ToShowBranchesItems(
        IEnumerable<Branch> branches, bool canBeOutside = false, bool includeAmbiguous = false)
    {
        return branches
            .Where(b => branches.ContainsBy(bb => bb.Name != b.RemoteName) && // Skip local if remote
                branches.ContainsBy(bb => bb.Name != b.PullMergeParentBranchName)) // Skip pull merge if main
            .DistinctBy(b => b.Name)
            .Select(b => Menu.Item(ToBranchMenuName(b, repo.RowCommit, canBeOutside), "",
                () => cmds.ShowBranch(b.Name, includeAmbiguous)));
    }


    string ToBranchMenuName(Branch branch, Commit cic, bool canBeOutside, bool isShowShown = true)
    {
        bool isBranchIn = false;
        bool isBranchOut = false;
        if (canBeOutside && !repo.Repo.BranchByName.TryGetValue(branch.Name, out var _))
        {   // The branch is currently not shown
            if (repo.Repo.AugmentedRepo.Branches.TryGetValue(branch.Name, out var b))
            {
                // The branch is not shown, but does exist
                if (cic.ParentIds.Count > 1 &&
                    repo.Repo.AugmentedRepo.CommitById[cic.ParentIds[1]].BranchName == b.Name)
                {   // Is a branch merge in '╮' branch                     
                    isBranchIn = true;
                }
                else if (cic.AllChildIds.ContainsBy(id =>
                     repo.Repo.AugmentedRepo.CommitById[id].BranchName == b.Name))
                {   // Is branch out '╯' branch
                    isBranchOut = true;
                }
            }
        }

        return ToBranchMenuName(branch, isBranchIn, isBranchOut, isShowShown);
    }

    string ToBranchMenuName(Branch branch, bool isBranchIn = false, bool isBranchOut = false, bool isShowShown = true)
    {
        var isShown = isShowShown && repo.Repo.BranchByName.TryGetValue(branch.Name, out var _);
        string name = branch.NiceNameUnique;

        name = branch.IsGitBranch ? " " + branch.NiceNameUnique : "~" + name;
        name = isBranchIn ? "╮" + name : name;
        name = isBranchOut ? "╯" + name : name;
        name = isBranchIn || isBranchOut ? name : " " + name;
        name = branch.IsCurrent || branch.IsLocalCurrent ? "●" + name : " " + name;
        name = isShown ? "╾" + name : " " + name;

        return name.Replace('_', '-');
    }

    // 
    bool IsAncestor(Branch b1, Branch? b2)
    {
        if (b2 == null) return false;
        return b2.AncestorNames.Contains(b1.Name);
    }


    string Sid(string id) => id == Repo.UncommittedId ? "uncommitted" : id.Sid();
}
