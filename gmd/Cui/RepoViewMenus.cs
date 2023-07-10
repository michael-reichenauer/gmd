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
            .Item("Toggle Details ...", "Enter", () => cmds.ToggleDetails())
            .Item("Commit ...", "C",
                () => cmds.CommitFromMenu(false),
                () => !repo.Status.IsOk)
            .Item($"Amend {currentSidText} ...", "A",
                () => cmds.CommitFromMenu(true),
                () => repo.GetCurrentCommit().IsAhead)
            .SubMenu("Undo", "", GetUndoItems())
            .SubMenu("Diff", "", GetDiffItems())
            .SubMenu("Open/Show Branch", "→", GetShowBranchItems())
            .SubMenu("Hide Branch", "←", GetHideItems())
            .SubMenu("Switch/Checkout", "", GetSwitchToItems()
                .Append(Menu.Item($"Switch to Commit {Sid(repo.RowCommit.Id)}", "", () => cmds.SwitchToCommit(), () => repo.Status.IsOk && repo.RowCommit.Id != repo.GetCurrentCommit().Id)))
            .SubMenu("Push/Publish", "", GetPushItems(), () => cmds.CanPush())
            .SubMenu("Pull/Update", "", GetPullItems(), () => cmds.CanPull())
            .SubMenu($"Merge from", "", GetMergeFromItems())
            .Item("Create Branch ...", "B", () => cmds.CreateBranch())
            .Item("Create Branch from commit ...", "",
                () => cmds.CreateBranchFromCommit(), () => repo.Status.IsOk)
            .SubMenu("Delete Branch", "", GetDeleteItems())
            .SubMenu("Stash", "", GetStashMenuItems())
            .SubMenu("Tag", "", GetTagItems())
            .SubMenu("Resolve Ambiguity", "", GetAmbiguousItems())
            .Item("Search/Filter ...", "F", () => cmds.Filter())
            .Item("Refresh/Reload", "R", () => cmds.RefreshAndFetch())
            .Item("File History ...", "", () => cmds.ShowFileHistory())
            .SubMenu("Open/Clone Repo", "O", GetOpenRepoItems())
            .Item("Change Branch Color", "G", () => cmds.ChangeBranchColor(), () => !repo.Branch(repo.RowCommit.BranchName).IsMainBranch)
            .SubMenu("Set Branch", "", GetSetBranchItems())
            .SubMenu("Move Branch left/right", "", GetMoveBranchItems())
            .Item("Help ...", "H", () => cmds.ShowHelp())
            .Item("Config ...", "", () => configDlg.Show(repo.RepoPath))
            .Item("About ...", "", () => cmds.ShowAbout())
            .Item("Quit", "Esc", () => UI.Shutdown());
    }

    IEnumerable<MenuItem> GetTagItems()
    {
        return Menu.Items
            .Item("Add Tag ...", "", () => cmds.AddTag(), () => !repo.RowCommit.IsUncommitted)
            .SubMenu("Remove Tag", "", GetDeleteTagItems());
    }

    IEnumerable<MenuItem> GetDeleteTagItems()
    {
        var commit = repo.RowCommit;
        return commit.Tags.Select(t => Menu.Item(t.Name, "", () => cmds.DeleteTag(t.Name)));
    }


    IEnumerable<MenuItem> GetDiffItems()
    {
        return Menu.Items
            .Item("Commit Diff ...", "D", () => cmds.ShowCurrentRowDiff())
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
        var rowCommonName = repo.RowBranch.CommonName;
        var rowBranches = repo.Branches.Where(b => b.CommonName == rowCommonName);

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
            if (b.B.CommonName == rowCommonName) break;
            leftBranch = b.B;
        }
        var leftCommonName = leftBranch != null && !IsAncestor(leftBranch, repo.RowBranch) ? leftBranch.CommonName : "";

        // Find possible branch on right side to move to after (skip if ancestor)
        Branch? rightBranch = null;
        for (int i = overlappingBranches.Count - 1; i >= 0; i--)
        {
            var b = overlappingBranches[i];
            if (b.B.CommonName == rowCommonName) break;
            rightBranch = b.B;
        }
        var rightCommonName = rightBranch != null && !IsAncestor(repo.RowBranch, rightBranch) ? rightBranch.CommonName : "";

        // Add menu items if movable branches found
        if (leftCommonName != "")
        {
            items.Item($"<= (Move {repo.RowBranch.NiceNameUnique} left of {leftBranch!.NiceNameUnique})", "", () => cmds.MoveBranch(repo.RowBranch.CommonName, leftCommonName, -1));
        }
        if (rightCommonName != "")
        {
            items.Item($"=> (Move {repo.RowBranch.NiceNameUnique} right of {rightBranch!.NiceNameUnique})", "", () => cmds.MoveBranch(repo.RowBranch.CommonName, rightCommonName, +1));
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
                .DistinctBy(b => b.CommonName)
                .Select(b => Menu.Item(ToBranchMenuName(b), "", () => cmds.ResolveAmbiguity(branch, b.NiceName))));
    }


    IEnumerable<MenuItem> GetSetBranchItems()
    {
        var commit = repo.RowCommit;
        return Menu.Items
            .Item(commit.ChildIds.Count() > 1, "Set Branch ...", "", () => cmds.SetBranchManuallyAsync())
            .Item(commit.IsBranchSetByUser, "Undo Set Branch", "", () => cmds.UndoSetBranch(commit.Id));
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
             .Where(b => b.CommonName != currentName && b.LocalName == "" && b.PullMergeParentBranchName == "")
             .OrderBy(b => b.CommonName);

        return ToSwitchBranchesItems(branches);
    }

    IEnumerable<MenuItem> GetDeleteItems()
    {
        var branches = repo.GetAllBranches()
            .Where(b => b.IsGitBranch && !b.IsMainBranch && !b.IsCurrent && !b.IsLocalCurrent
                && b.LocalName == "" && b.PullMergeParentBranchName == "")
            .OrderBy(b => repo.Branches.ContainsBy(bb => bb.CommonName == b.CommonName) ? 0 : 1)
            .ThenBy(b => b.CommonName);

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
        var currentName = repo.CurrentBranch?.CommonName ?? "";

        // Get all branches except current
        var branches = repo.Branches
             .Where(b => b.CommonName != currentName && b.LocalName == "" && b.PullMergeParentBranchName == "")
             .DistinctBy(b => b.TipId)
             .OrderBy(b => b.CommonName);

        // Include commit if not on current branch
        var commitItems = repo.Branch(commit.BranchName) != repo.CurrentBranch
            ? Menu.Items.Item($"Commit {commit.Sid}", "", () => cmds.MergeBranch(commit.Id))
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
        var currentName = repo.CurrentBranch?.CommonName ?? "";
        var branches = repo.Branches
             .Where(b => b.CommonName != currentName &&
                b.RemoteName == "" && b.PullMergeParentBranchName == "")
             .DistinctBy(b => b.NiceNameUnique)
             .OrderBy(b => b.NiceNameUnique);

        return branches.Select(b => Menu.Item(ToBranchMenuName(b, false, false, false), "", () => cmds.DiffWithOtherBranch(b.Name, isFromCurrentCommit, isSwitch)));
    }

    IEnumerable<MenuItem> GetHideItems()
    {
        var mainBranch = repo.Branches.First(b => b.IsMainBranch);
        var branches = repo.Branches
            .Where(b => !b.IsMainBranch && !b.IsDetached && b.CommonName != mainBranch.CommonName &&
                b.RemoteName == "" && b.PullMergeParentBranchName == "")
            .DistinctBy(b => b.NiceNameUnique)
            .OrderBy(b => b.NiceNameUnique);

        return ToHideHiarchicalBranchesItems(branches);
    }


    IEnumerable<MenuItem> GetShowBranchItems()
    {
        var allBranches = repo.GetAllBranches();

        var liveBranches = allBranches
            .Where(b => b.IsGitBranch)
            .Where(b => b.RemoteName == "" && b.PullMergeParentBranchName == "")
            .OrderBy(b => b.NiceNameUnique);

        var liveAndDeletedBranches = allBranches
            .Where(b => b.RemoteName == "" && b.PullMergeParentBranchName == "")
            .OrderBy(b => b.NiceNameUnique)
            .ToList();

        var recentBranches = liveAndDeletedBranches
            .Where(b => b.RemoteName == "" && b.PullMergeParentBranchName == "")
            .OrderBy(b => repo.Repo.AugmentedRepo.CommitById[b.TipId].GitIndex)
            .Take(RecentCount);

        var ambiguousBranches = allBranches
            .Where(b => b.AmbiguousTipId != "")
            .OrderBy(b => b.NiceNameUnique);

        var items = Menu.Items
            .SubMenu("Recent", "", ToShowHiarchicalBranchesItems(recentBranches))
            .SubMenu("Active", "", ToShowHiarchicalBranchesItems(liveBranches))
            .SubMenu("Active and Deleted", "", ToShowHiarchicalBranchesItems(liveAndDeletedBranches));

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


    IEnumerable<MenuItem> ToHideHiarchicalBranchesItems(IEnumerable<Branch> branches)
    {
        if (branches.Count() <= MaxItemCount)
        {   // Too few branches to bother with submenus
            return ToHideBranchesItems(branches);
        }

        var cic = repo.RowCommit;

        // Group by first part of the b.commonName (if '/' exists in name)
        var groups = branches
            .GroupBy(b => b.CommonName.Split('/')[0])
            .OrderBy(g => g.Key)
            .OrderBy(g => g.Count() > 1 ? 0 : 1);  // Sort groups first;

        // If only one item in group, then just show branch, otherwise show submenu
        return groups.Select(g =>
            g.Count() == 1
                ? Menu.Item(ToBranchMenuName(g.First(), cic, false), "", () => cmds.HideBranch(g.First().Name))
                : Menu.SubMenu($"    {g.Key}/┅", "", ToHideBranchesItems(g)))
            .ToList();
    }

    IEnumerable<MenuItem> ToShowHiarchicalBranchesItems(IEnumerable<Branch> branches)
    {
        if (branches.Count() <= MaxItemCount)
        {   // Too few branches to bother with submenus
            return ToShowBranchesItems(branches, false, false);
        }

        var cic = repo.RowCommit;

        // Group by first part of the b.commonName (if '/' exists in name)
        var groups = branches
            .GroupBy(b => b.NiceNameUnique.Split('/', '(')[0])
            .OrderBy(g => g.Key)
            .OrderBy(g => g.Count() > 1 ? 0 : 1);  // Sort groups first;

        // If only one item in group, then just show branch, otherwise show submenu
        return groups.Select(g =>
            g.Count() == 1
                ? Menu.Item(ToBranchMenuName(g.First(), cic, false), "", () => cmds.ShowBranch(g.First().Name, false))
                : Menu.SubMenu($"    {g.Key}/┅", "", ToShowBranchesItems(g, false, false)));
    }

    IEnumerable<MenuItem> ToShowBranchesItems(
        IEnumerable<Branch> branches, bool canBeOutside = false, bool includeAmbiguous = false)
    {
        var cic = repo.RowCommit;
        return branches
            .Where(b => branches.ContainsBy(bb => bb.Name != b.RemoteName) && // Skip local if remote
                branches.ContainsBy(bb => bb.Name != b.PullMergeParentBranchName)) // Skip pull merge if main
            .DistinctBy(b => b.Name)
            .Select(b => Menu.Item(ToBranchMenuName(b, cic, canBeOutside), "",
                () => cmds.ShowBranch(b.Name, includeAmbiguous)));
    }


    IEnumerable<MenuItem> ToSwitchBranchesItems(IEnumerable<Branch> branches)
    {
        var cic = repo.RowCommit;
        return branches
            .Select(b => Menu.Item(ToBranchMenuName(b, cic, false, false), "", () => cmds.SwitchTo(b.Name)));
    }

    string ToBranchMenuName(Branch branch, Commit cic, bool canBeOutside, bool isShowShown = true)
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

    bool IsAncestor(Branch b1, Branch? b2)
    {
        while (b2 != null)
        {
            if (b2.CommonName == b1.CommonName)
            {
                return true;
            }
            b2 = repo.Branches.FirstOrDefault(b => b.Name == b2.ParentBranchName);
        }

        return false;
    }


    string Sid(string id) => id == Repo.UncommittedId ? "uncommitted" : id.Sid();
}
