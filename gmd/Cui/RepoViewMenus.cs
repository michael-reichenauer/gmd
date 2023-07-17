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
        Menu.Show("Main Menu", -1, 0, GetMainMenuItems());
    }


    public void ShowShowBranchesMenu()
    {
        Menu.Show("Switch to Branch", repo.CurrentPoint.X, repo.CurrentPoint.Y + 1, Menu.Items
            .Items(GetSwitchToItems())
            .Separator("Show/Open Branch")
            .Items(GetShowBranchItems())
            .Separator()
            .SubMenu("Main Menu", "M", GetMainMenuItems()));
    }

    public void ShowHideBranchesMenu()
    {
        Menu.Show("Close/Hide", repo.CurrentPoint.X, repo.CurrentPoint.Y + 1, GetHideItems());
    }

    public void ShowOpenMenu()
    {
        Menu.Show("Open Repo", -1, 0, GetOpenRepoItems());
    }

    IEnumerable<MenuItem> GetMainMenuItems()
    {
        var releases = states.Get().Releases;
        var items = Menu.Items;
        var branchName = repo.CurrentBranch?.NiceNameUnique ?? "";
        var commit = repo.RowCommit;
        var sidText = Sid(repo.RowCommit.Id);
        var currentSidText = Sid(repo.GetCurrentCommit().Sid);
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
           .Item("Set Branch Manually ...", "", () => cmds.SetBranchManuallyAsync())
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
            .SelectMany(b => repo.Graph.GetOverlappingBranches(b.Name))
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
                .Select(b => Menu.Item(ToBranchMenuName(b, false), "", () => cmds.ResolveAmbiguity(branch, b.NiceName))));
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

    IEnumerable<MenuItem> GetCommitInOutItems()
    {
        // Get current branch, commit branch in/out and all shown branches
        var shownBranches = repo.Branches;
        var branches =
            new Branch[0]
            .Concat(repo.GetCommitBranches())
            .Concat(repo.Branches);
        var currentBranch = repo.GetCurrentBranch();
        if (currentBranch != null && !branches.ContainsBy(b => b.PrimaryName == currentBranch.PrimaryName))
        {
            branches = branches.Prepend(currentBranch);
        }
        branches = branches.Where(b => !repo.Branches.ContainsBy(bb => bb.PrimaryName == b.PrimaryName));

        return ToBranchesItems(branches, b => cmds.ShowBranch(b.Name, false), null, true);
    }


    IEnumerable<MenuItem> GetSwitchToItems()
    {
        var currentName = repo.CurrentBranch?.PrimaryName ?? "";
        var branches = repo.Branches
             .Where(b => b.PrimaryName != currentName)
             .OrderBy(b => b.PrimaryName);

        return ToHierarchicalBranchesItems(branches, b => cmds.SwitchTo(b.LocalName != "" ? b.LocalName : b.Name), null, true);
    }

    IEnumerable<MenuItem> GetDeleteItems()
    {
        var branches = repo.GetAllBranches()
            .Where(b => b.IsGitBranch && !b.IsMainBranch && !b.IsCurrent && !b.IsLocalCurrent)
            .OrderBy(b => repo.Branches.ContainsBy(bb => bb.PrimaryName == b.PrimaryName) ? 0 : 1)
            .ThenBy(b => b.PrimaryName);

        return ToHierarchicalBranchesItems(branches, b => cmds.DeleteBranch(b.Name))
            .Select(i => i with { Title = $"{i.Title} ..." });
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

        // Include cherry pic if not on current branch
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

        return branches.Select(b => Menu.Item(ToBranchMenuName(b), "", () => cmds.DiffWithOtherBranch(b.Name, isFromCurrentCommit, isSwitch)));
    }

    IEnumerable<MenuItem> GetHideItems()
    {
        var branches = repo.Branches
            .Where(b => !b.IsMainBranch && !b.IsDetached)
            .OrderBy(b => b.NiceNameUnique);

        var items = ToHierarchicalBranchesItems(branches, b => cmds.HideBranch(b.Name), null, true);
        if (repo.Branches.Count > 15)
        {
            items = items.Prepend(Menu.Item("Hide All", "", () => cmds.HideBranch("", true)));
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

        var show = (Branch b) => cmds.ShowBranch(b.Name, false);

        var items = Menu.Items
            .Items(GetCommitInOutItems())
            .SubMenu("    Recent", "", ToBranchesItems(recentBranches, show)
                .Prepend(Menu.Item("Show 5 more Recent", "", () => cmds.ShowBranch("", false, ShowBranches.AllRecent, 5))))
            .SubMenu("    Active", "", ToHierarchicalBranchesItems(liveBranches, show)
                .Prepend(Menu.Item("Show All Active", "", () => cmds.ShowBranch("", false, ShowBranches.AllActive))))
            .SubMenu("    Active and Deleted", "", ToHierarchicalBranchesItems(liveAndDeletedBranches, show)
                .Prepend(Menu.Item("Show All Active and Deleted", "", () => cmds.ShowBranch("", false, ShowBranches.AllActiveAndDeleted))));

        return ambiguousBranches.Any()
            ? items.SubMenu("   Ambiguous", "", ToBranchesItems(ambiguousBranches, b => cmds.ShowBranch(b.Name, true)))
            : items;
    }


    IEnumerable<MenuItem> GetUndoItems()
    {
        string id = repo.RowCommit.Id;
        string sid = repo.RowCommit.Sid;
        var binaryPaths = repo.Status.AddedFiles
            .Concat(repo.Status.ModifiedFiles)
            .Concat(repo.Status.RenamedTargetFiles)
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


    IEnumerable<MenuItem> ToHierarchicalBranchesItems(
        IEnumerable<Branch> branches, Action<Branch> action, Func<Branch, bool>? canExecute = null, bool isNoShowIcon = false)
    {
        var filteredBranches = branches.Where(b => b.IsPrimary).DistinctBy(b => b.PrimaryName);
        return ToHierarchicalBranchesItemsImpl(filteredBranches, action, canExecute, isNoShowIcon, 0);
    }


    IEnumerable<MenuItem> ToHierarchicalBranchesItemsImpl(
           IEnumerable<Branch> branches, Action<Branch> action, Func<Branch, bool>? canExecute, bool isNoShowIcon, int level)
    {
        if (branches.Count() <= MaxItemCount)
        {   // Too few branches to bother with submenus
            return ToBranchesItems(branches, action, canExecute, false, isNoShowIcon);
        }

        // Group by first part of the b.commonName (if '/' exists in name)
        var groups = branches
            .GroupBy(b =>
            {
                var parts = b.NiceNameUnique.Split('/', '(');
                if (parts.Length <= level) return parts.Last();
                return parts[level];
            })
            .OrderBy(g => g.Count() > 1 ? 0 : 1) // Sort groups first;
            .ThenBy(g => g.Key);

        // If only one item in group, then just show branch, otherwise show submenu
        // Group name is either group/* or group(*) depending on if all branches in group have same nice name
        var toGroupName = (IGrouping<string, Branch> bs) => bs.All(b => b.NiceName == bs.First().NiceName)
            ? $"    {bs.Key}(*)" : $"    {bs.Key}/*";
        return groups.Select(g =>
            g.Count() == 1
                ? ToBranchesItems(g, action, canExecute, false, isNoShowIcon).First()
                : Menu.SubMenu(toGroupName(g), "", ToHierarchicalBranchesItemsImpl(g, action, canExecute, isNoShowIcon, level + 1)));
    }


    IEnumerable<MenuItem> ToBranchesItems(
        IEnumerable<Branch> branches,
        Action<Branch> action,
        Func<Branch, bool>? canExecute = null,
        bool canBeOutside = false,
        bool isNoShowIcon = false)
    {
        canExecute = canExecute ?? (b => true);
        return branches.Select(b => Menu.Item(ToBranchMenuName(b, canBeOutside, isNoShowIcon), "",
            () => action(b), () => canExecute(b)));
    }


    string ToBranchMenuName(Branch branch, bool canBeOutside = false, bool isNoShowIcon = false)
    {
        var cic = repo.RowCommit;
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

        var isShown = !isNoShowIcon && repo.Repo.BranchByName.TryGetValue(branch.Name, out var _);
        string name = branch.NiceNameUnique;

        name = branch.IsGitBranch ? " " + branch.NiceNameUnique : "~" + name;
        name = isBranchIn ? "╮" + name : name;
        name = isBranchOut ? "╯" + name : name;
        name = isBranchIn || isBranchOut ? name : " " + name;
        name = branch.IsCurrent || branch.IsLocalCurrent ? "●" + name : " " + name;
        name = isShown ? "∘" + name : " " + name;

        return name;
    }


    bool IsAncestor(Branch b1, Branch? b2)
    {
        if (b2 == null) return false;
        return b2.AncestorNames.Contains(b1.Name);
    }


    string Sid(string id) => id == Repo.UncommittedId ? "uncommitted" : id.Sid();
}
