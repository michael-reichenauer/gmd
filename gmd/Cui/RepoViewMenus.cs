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
            .Add(GetSwitchToItems())
            .AddSeparator("Show/Open")
            .Add(GetShowItems())
            .AddSeparator()
            .AddSubMenu("Show Branch", "", GetShowBranchItems())
            .AddSubMenu("Main Menu", "M", GetMainMenuItems()),
            "Switch to");
    }

    public void ShowHideBranchesMenu()
    {
        Menu.Show(repo.CurrentPoint.X, repo.CurrentPoint.Y + 1, GetHideItems(), "Close/Hide");
    }

    public void ShowOpenMenu()
    {
        int x = repo.ContentWidth / 2 - 10;
        Menu.Show(x, 0, GetOpenRepoItems(), "Open Repo");
    }

    IEnumerable<MenuItem> GetMainMenuItems()
    {
        using (Timing.Start())
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
                items.AddSeparator("New Release Available !!!")
                    .AddItem("Update to Latest ...", "", () => cmds.UpdateRelease())
                    .AddSeparator();
            }

            return items
                .AddItem("Toggle Details ...", "Enter", () => cmds.ToggleDetails())
                .AddItem("Commit ...", "C",
                    () => cmds.CommitFromMenu(false),
                    () => !repo.Status.IsOk)
                .AddItem($"Amend {currentSidText} ...", "A",
                    () => cmds.CommitFromMenu(true),
                    () => repo.GetCurrentCommit().IsAhead)
                .AddSubMenu("Undo", "", GetUndoItems())
                .AddSubMenu("Diff", "", GetDiffItems())
                .AddSubMenu("Open/Show Branch", "→", GetShowBranchItems())
                .AddSubMenu("Hide Branch", "←", GetHideItems())
                .AddSubMenu("Switch/Checkout", "", GetSwitchToItems()
                    .Append(new MenuItem($"Switch to Commit {Sid(repo.RowCommit.Id)}", "", () => cmds.SwitchToCommit(), () => repo.Status.IsOk && repo.RowCommit.Id != repo.GetCurrentCommit().Id)))
                .AddSubMenu("Push/Publish", "", GetPushItems(), () => cmds.CanPush())
                .AddSubMenu("Pull/Update", "", GetPullItems(), () => cmds.CanPull())
                .AddSubMenu($"Merge from", "", GetMergeFromItems())
                .AddItem("Create Branch ...", "B", () => cmds.CreateBranch())
                .AddItem("Create Branch from commit ...", "",
                    () => cmds.CreateBranchFromCommit(), () => repo.Status.IsOk)
                .AddSubMenu("Delete Branch", "", GetDeleteItems())
                .AddSubMenu("Stash", "", GetStashMenuItems())
                .AddSubMenu("Tag", "", GetTagItems())
                .AddSubMenu("Resolve Ambiguity", "", GetAmbiguousItems())
                .AddItem("Search/Filter ...", "F", () => cmds.Filter())
                .AddItem("Refresh/Reload", "R", () => cmds.RefreshAndFetch())
                .AddItem("File History ...", "", () => cmds.ShowFileHistory())
                .AddSubMenu("Open/Clone Repo", "O", GetOpenRepoItems())
                .AddItem("Change Branch Color", "G", () => cmds.ChangeBranchColor(), () => !repo.Branch(repo.RowCommit.BranchName).IsMainBranch)
                .AddSubMenu("Set Branch", "", GetSetBranchItems())
                .AddSubMenu("Move Branch left/right", "", GetMoveBranchItems())
                .AddItem("Help ...", "H", () => cmds.ShowHelp())
                .AddItem("Config ...", "", () => configDlg.Show(repo.RepoPath))
                .AddItem("About ...", "", () => cmds.ShowAbout())
                .AddItem("Quit", "Esc", () => UI.Shutdown());
        }
    }

    IEnumerable<MenuItem> GetTagItems()
    {
        return new List<MenuItem>()
            .AddItem("Add Tag ...", "", () => cmds.AddTag(), () => !repo.RowCommit.IsUncommitted)
            .AddSubMenu("Remove Tag", "", GetDeleteTagItems());
    }

    IEnumerable<MenuItem> GetDeleteTagItems()
    {
        var commit = repo.RowCommit;
        return commit.Tags.Select(t => new MenuItem(t.Name, "", () => cmds.DeleteTag(t.Name)));
    }


    IEnumerable<MenuItem> GetDiffItems()
    {
        return Menu.Items
            .AddItem("Commit Diff ...", "D", () => cmds.ShowCurrentRowDiff())
            .AddSubMenu($"Diff Branch to", "", GetPreviewMergeItems(false, false))
            .AddSubMenu($"Diff {Sid(repo.RowCommit.Id)} to", "", GetPreviewMergeItems(true, false))
            .AddSubMenu($"Diff Branch from", "", GetPreviewMergeItems(false, true))
            .AddItem($"Diff {Sid(repo.RowCommit.Id)} from", "", () => cmds.DiffWithOtherBranch(repo.RowCommit.BranchName, true, true), () => repo.Status.IsOk)
            .AddSubMenu("Stash Diff", "", GetStashDiffItems());
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
            items.AddItem($"<= (Move {repo.RowBranch.NiceNameUnique} left of {leftBranch!.NiceNameUnique})", "", () => cmds.MoveBranch(repo.RowBranch.CommonName, leftCommonName, -1));
        }
        if (rightCommonName != "")
        {
            items.AddItem($"=> (Move {repo.RowBranch.NiceNameUnique} right of {rightBranch!.NiceNameUnique})", "", () => cmds.MoveBranch(repo.RowBranch.CommonName, rightCommonName, +1));
        }

        return items;
    }


    IEnumerable<MenuItem> GetStashMenuItems()
    {
        return Menu.Items
         .AddItem("Stash Changes", "", () => cmds.Stash(), () => !repo.Status.IsOk)
         .AddSubMenu("Stash Pop", "", GetStashPopItems(), () => repo.Status.IsOk)
         .AddSubMenu("Stash Diff", "", GetStashDiffItems())
         .AddSubMenu("Stash Drop", "", GetStashDropItems());
    }

    IEnumerable<MenuItem> GetStashPopItems()
    {
        return repo.Repo.Stashes.Select(s =>
            new MenuItem($"{s.Message}", "", () => cmds.StashPop(s.Name)));
    }

    IEnumerable<MenuItem> GetStashDropItems()
    {
        return repo.Repo.Stashes.Select(s =>
            new MenuItem($"{s.Message}", "", () => cmds.StashDrop(s.Name)));
    }

    IEnumerable<MenuItem> GetStashDiffItems()
    {
        return repo.Repo.Stashes.Select(s =>
            new MenuItem($"{s.Message}", "", () => cmds.StashDiff(s.Name)));
    }
    IEnumerable<MenuItem> GetConfigItems()
    {
        var path = repo.RepoPath;
        var previewTxt = config.Get().AllowPreview ? "Disable Preview Releases" : "Enable Preview Releases";
        var metaSyncTxt = repoConfig.Get(path).SyncMetaData ? "Disable this Repo Sync Metadata" : "Enable this Repo Sync Metadata";

        return Menu.Items
             .AddItem(previewTxt, "", () =>
             {
                 config.Set(c => c.AllowPreview = !c.AllowPreview);
                 updater.CheckUpdateAvailableAsync().RunInBackground();
             })
             .AddItem(metaSyncTxt, "", () => repoConfig.Set(path, c => c.SyncMetaData = !c.SyncMetaData));
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
            items.AddItem("Undo Set Branch", "", () => cmds.UndoSetBranch(commit.Id));
        }

        var branch = repo.Branch(commit.BranchName);
        return items
            .Concat(branch.AmbiguousBranchNames.Select(n => repo.AllBranchByName(n))
                .DistinctBy(b => b.CommonName)
                .Select(b => new MenuItem(ToBranchMenuName(b), "", () => cmds.ResolveAmbiguity(branch, b.NiceName))));
    }


    IEnumerable<MenuItem> GetSetBranchItems()
    {
        var items = Menu.Items;
        var commit = repo.RowCommit;

        if (commit.ChildIds.Count() > 1)
        {
            items.AddItem("Set Branch ...", "", () => cmds.SetBranchManuallyAsync());
        }
        if (commit.IsBranchSetByUser)
        {
            items.AddItem("Undo Set Branch", "", () => cmds.UndoSetBranch(commit.Id));
        }

        return items;
    }


    IEnumerable<MenuItem> GetPushItems()
    {
        var items = Menu.Items;

        items.AddItem("Push All Branches", "P", () => cmds.PushAllBranches());

        if (repo.CurrentBranch != null)
        {
            items.AddItem(ToBranchMenuName(repo.CurrentBranch), "p",
                    () => cmds.PushCurrentBranch(),
                    () => cmds.CanPushCurrentBranch());
        }
        items.Add(repo.Branches
            .Where(b => !b.IsRemote && !b.IsCurrent && b.HasLocalOnly && !b.HasRemoteOnly)
            .Select(b => (new MenuItem($"Push {ToBranchMenuName(b)}", "", () => cmds.PushBranch(b.Name)))));

        if (repo.CurrentBranch != null && repo.CurrentBranch.RemoteName == "")
        {
            items.AddItem($"Publish {repo.CurrentBranch.NiceNameUnique}", "",
            () => cmds.PublishCurrentBranch());
        }

        return items.DistinctBy(b => b.Title);
    }


    IEnumerable<MenuItem> GetOpenRepoItems()
    {
        return Menu.Items
            .Add(GetRecentRepoItems())
            .AddSeparator()
            .AddItem("Browse ...", "", () => cmds.ShowBrowseDialog())
            .AddItem("Clone ...", "", () => cmds.Clone(), () => true);
    }


    IEnumerable<MenuItem> GetRecentRepoItems() =>
        states.Get().RecentFolders
            .Where(Files.DirExists)
            .Take(10)
            .Select(path => new MenuItem(path, "", () => cmds.ShowRepo(path), () => path != repo.RepoPath));


    IEnumerable<MenuItem> GetPullItems()
    {
        var items = Menu.Items;
        items.AddItem("Update/Pull All Branches", "U", () => cmds.PullAllBranches());
        if (repo.CurrentBranch != null)
        {
            items.AddItem(ToBranchMenuName(repo.CurrentBranch), "u",
                    () => cmds.PullCurrentBranch(),
                    () => cmds.CanPullCurrentBranch());
        }
        items.Add(repo.Branches
            .Where(b => b.IsRemote && !b.IsCurrent && b.HasRemoteOnly)
            .Select(b => (new MenuItem($"{ToBranchMenuName(b)}", "", () => cmds.PullBranch(b.Name)))));
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


    IEnumerable<MenuItem> ToDeleteItems(IEnumerable<Branch> branches)
    {
        return branches.Select(b =>
                new MenuItem($"{ToBranchMenuName(b)} ...", "", () => cmds.DeleteBranch(b.Name)));
    }

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
                ? new MenuItem($"{ToBranchMenuName(g.First())} ...", "", () => cmds.DeleteBranch(g.First().Name))
                : new SubMenu($"    {g.Key}/┅", "", ToDeleteItems(g)));
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
            ? Menu.Items.AddItem($"Commit {commit.Sid}", "", () => cmds.MergeBranch(commit.Id))
            : Menu.Items;

        // Incluce cherry pic if not on current branch
        var cherryPicItems = repo.RowCommit.Id != repo.CurrentBranch?.TipId
            ? Menu.Items.AddItem($"Cherry Pic {sidText}", "", () => cmds.CherryPic(commit.Id), () => repo.Status.IsOk)
            : Menu.Items;

        var items = branches
            .Select(b => new MenuItem(ToBranchMenuName(b), "", () => cmds.MergeBranch(b.Name)))
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

        return branches.Select(b => new MenuItem(ToBranchMenuName(b, false, false, false), "", () => cmds.DiffWithOtherBranch(b.Name, isFromCurrentCommit, isSwitch)));
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
        using (Timing.Start())
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

            var items = new List<MenuItem>()
                .AddSubMenu("Recent", "", ToShowHiarchicalBranchesItems(recentBranches))
                .AddSubMenu("Active", "", ToShowHiarchicalBranchesItems(liveBranches))
                .AddSubMenu("Active and Deleted", "", ToShowHiarchicalBranchesItems(liveAndDeletedBranches));

            return ambiguousBranches.Any()
                ? items.AddSubMenu("Ambiguous", "", ToShowBranchesItems(ambiguousBranches, false, true))
                : items;
        }
    }


    IEnumerable<MenuItem> GetUndoItems()
    {
        string id = repo.RowCommit.Id;
        string sid = repo.RowCommit.Sid;
        var binaryPaths = repo.Status.AddedFiles.Concat(repo.Status.ModifiedFiles)
                .Where(f => !Files.IsText(Path.Join(repo.RepoPath, f)))
                .ToList();

        return new List<MenuItem>()
            .AddSubMenu("Undo/Restore Uncommitted File", "", GetUncommittedFileItems(), () => cmds.CanUndoUncommitted())
            .AddItem($"Undo Commit {sid}", "", () => cmds.UndoCommit(id), () => cmds.CanUndoCommit())
            .AddItem($"Uncommit Last Commit", "", () => cmds.UncommitLastCommit(), () => cmds.CanUncommitLastCommit())
            .AddSeparator()
            .AddItem("Undo/Restore all Uncommitted Binary Files", "", () => cmds.UndoUncommittedFiles(binaryPaths), () => binaryPaths.Any())
            .AddItem("Undo/Restore all Uncommitted Changes", "",
                () => cmds.UndoAllUncommittedChanged(), () => cmds.CanUndoUncommitted())
            .AddItem("Clean/Restore Working Folder", "", () => cmds.CleanWorkingFolder());
    }


    IEnumerable<MenuItem> GetUncommittedFileItems() =>
        repo.GetUncommittedFiles().Select(f => new MenuItem(f, "", () => cmds.UndoUncommittedFile(f)));

    IEnumerable<MenuItem> ToHideBranchesItems(IEnumerable<Branch> branches)
    {
        return branches.Select(b =>
                new MenuItem(ToBranchMenuName(b, false, false, false), "", () => cmds.HideBranch(b.Name)));
    }


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
                ? new MenuItem(ToBranchMenuName(g.First(), cic, false), "", () => cmds.HideBranch(g.First().Name))
                : new SubMenu($"    {g.Key}/┅", "", ToHideBranchesItems(g)))
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
                ? new MenuItem(ToBranchMenuName(g.First(), cic, false), "", () => cmds.ShowBranch(g.First().Name, false))
                : new SubMenu($"    {g.Key}/┅", "", ToShowBranchesItems(g, false, false)));
    }

    IEnumerable<MenuItem> ToShowBranchesItems(
        IEnumerable<Branch> branches, bool canBeOutside = false, bool includeAmbiguous = false)
    {
        var cic = repo.RowCommit;
        return branches
            .Where(b => branches.ContainsBy(bb => bb.Name != b.RemoteName) && // Skip local if remote
                branches.ContainsBy(bb => bb.Name != b.PullMergeParentBranchName)) // Skip pull merge if main
            .DistinctBy(b => b.Name)
            .Select(b => new MenuItem(ToBranchMenuName(b, cic, canBeOutside), "",
                () => cmds.ShowBranch(b.Name, includeAmbiguous)));
    }


    IEnumerable<MenuItem> ToSwitchBranchesItems(IEnumerable<Branch> branches)
    {
        var cic = repo.RowCommit;
        return branches
            .Select(b => new MenuItem(ToBranchMenuName(b, cic, false, false), "", () => cmds.SwitchTo(b.Name)));
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
