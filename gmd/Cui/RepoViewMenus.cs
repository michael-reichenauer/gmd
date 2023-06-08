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
}

class RepoViewMenus : IRepoViewMenus
{
    readonly int RecentCount = 15;
    readonly int HierachiacalCount = 15;
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
        Menu.Show(x, 0, GetMainMenuItems(x, 0));
    }

    public void ShowShowBranchesMenu()
    {
        Menu.Show(repo.CurrentPoint.X, repo.CurrentPoint.Y, Menu.Items()
            .Separator("Switch to")
            .Add(GetSwitchToItems())
            .Separator("Open")
            .Add(GetShowItems())
            .Separator("More")
            .Menu("Show Branch", "", GetShowBranchItems())
            .Menu("Main Menu", "m", GetMainMenuItems(repo.CurrentPoint.X, repo.CurrentPoint.Y)));
    }

    public void ShowHideBranchesMenu()
    {
        Menu.Show(repo.CurrentPoint.X, repo.CurrentPoint.Y, Menu.Items()
            .Separator("Close/Hide")
            .Add(GetHideItems()));
    }

    IEnumerable<MenuItem> GetMainMenuItems(int x, int y)
    {
        var releases = states.Get().Releases;
        var items = Menu.Items();
        var branchName = repo.CurrentBranch?.DisplayName ?? "";
        var commit = repo.RowCommit;
        var sidText = Sid(repo.RowCommit.Id);
        var currentSidText = Sid(repo.GetCurrentCommit().Sid);
        var curcom = repo.GetCurrentCommit();
        var isAhead = repo.GetCurrentCommit().IsAhead;

        if (releases.IsUpdateAvailable)
        {
            items.Separator("New Release Available !!!")
                .Item("Update to Latest ...", "", () => cmds.UpdateRelease())
                .Separator();
        }

        return items
            .Item("Toggle Details ...", "Enter", () => cmds.ToggleDetails())
            .Item("Commit ...", "c",
                () => cmds.CommitFromMenu(false),
                () => !repo.Status.IsOk)
            .Item($"Amend {currentSidText} ...", "a",
                () => cmds.CommitFromMenu(true),
                () => repo.GetCurrentCommit().IsAhead)
            .Menu("Undo", "", GetUndoItems())
            .Menu("Diff", "", GetDiffItems())
            .Menu("Open/Show Branch", "->", GetShowBranchItems())
            .Menu("Hide Branch", "<-", GetHideItems())
            .Menu("Switch/Checkout", "", GetSwitchToItems())
            .Menu("Push/Publish", "", GetPushItems(), () => cmds.CanPush())
            .Menu("Pull/Update", "", GetPullItems(), () => cmds.CanPull())
            .Menu($"Merge from", "", GetMergeFromItems(), () => GetMergeFromItems().Any())
            .Item("Create Branch ...", "b", () => cmds.CreateBranch())
            .Item("Create Branch from commit ...", "",
                () => cmds.CreateBranchFromCommit(), () => repo.Status.IsOk)
            .Menu("Delete Branch", "", GetDeleteItems())
            .Menu("Stash", "", GetStashMenuItems())
            .Menu("Tag", "", GetTagItems())
            .Menu("Resolve Ambiguity", "", GetAmbiguousItems(), () => GetAmbiguousItems().Any())

            .Separator("")
            .Menu("More", "", GetMoreItems())
            .Item("Quit", "Esc", () => UI.Shutdown());
    }

    IEnumerable<MenuItem> GetTagItems()
    {
        return Menu.Items()
            .Item("Add Tag ...", "", () => cmds.AddTag(), () => !repo.RowCommit.IsUncommitted)
            .Menu("Remove Tag", "", GetDeleteTagItems(), () => GetDeleteTagItems().Any());
    }

    IEnumerable<MenuItem> GetDeleteTagItems()
    {
        var commit = repo.RowCommit;
        return commit.Tags.Select(t => Menu.Item(t.Name, "", () => cmds.DeleteTag(t.Name)));
    }


    IEnumerable<MenuItem> GetDiffItems()
    {
        return Menu.Items()
            .Item("Commit Diff ...", "d", () => cmds.ShowCurrentRowDiff())
            .Menu($"Diff Branch to", "", GetPreviewMergeItems(false, false), () => GetPreviewMergeItems(false, false).Any())
            .Menu($"Diff {Sid(repo.RowCommit.Id)} to", "", GetPreviewMergeItems(true, false), () => GetPreviewMergeItems(true, false).Any())
            .Menu($"Diff Branch from", "", GetPreviewMergeItems(false, true), () => GetPreviewMergeItems(false, true).Any())
            .Item($"Diff {Sid(repo.RowCommit.Id)} from", "", () => cmds.DiffWithOtherBranch(repo.RowCommit.BranchName, true, true))
            .Menu("Stash Diff", "", GetStashDiffItems(), () => GetStashDiffItems().Any());
    }

    IEnumerable<MenuItem> GetMoreItems()
    {
        return Menu.Items()
            .Item("Search/Filter ...", "f", () => cmds.Filter())
            .Item("Refresh/Reload", "r", () => cmds.Refresh())
            .Item($"Switch to Commit {Sid(repo.RowCommit.Id)}", "", () => cmds.SwitchToCommit(), () => repo.Status.IsOk && repo.RowCommit.Id != repo.GetCurrentCommit().Id)
            .Item("File History ...", "", () => cmds.ShowFileHistory())
            .Menu("Open/Clone Repo", "", GetOpenRepoItems())
            .Item("Change Branch Color", "g", () => cmds.ChangeBranchColor(), () => !repo.Branch(repo.RowCommit.BranchName).IsMainBranch)
            .Menu("Set Branch", "", GetSetBranchItems(), () => GetSetBranchItems().Any())
            .Menu("Move Branch left/right", "", GetMoveBranchItems(), () => GetMoveBranchItems().Any())
            .Item("Help ...", "h", () => cmds.ShowHelp())
            .Item("Config ...", "", () => configDlg.Show(repo.RepoPath))
            .Item("About ...", "", () => cmds.ShowAbout());
    }

    IEnumerable<MenuItem> GetMoveBranchItems()
    {
        var items = Enumerable.Empty<MenuItem>();

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
            items = items.Append(Menu.Item($"<= (Move {repo.RowBranch.DisplayName} left of {leftBranch!.DisplayName})", "", () => cmds.MoveBranch(repo.RowBranch.CommonName, leftCommonName, -1)));
        }
        if (rightCommonName != "")
        {
            items = items.Append(Menu.Item($"=> (Move {repo.RowBranch.DisplayName} right of {rightBranch!.DisplayName})", "", () => cmds.MoveBranch(repo.RowBranch.CommonName, rightCommonName, +1)));
        }

        return items;
    }


    IEnumerable<MenuItem> GetStashMenuItems()
    {
        return EnumerableEx.From(
            Menu.Item("Stash Changes", "", () => cmds.Stash(), () => !repo.Status.IsOk),
            Menu.SubMenu("Stash Pop", "", GetStashPopItems(), () => repo.Status.IsOk && GetStashPopItems().Any()),
            Menu.SubMenu("Stash Diff", "", GetStashDiffItems(), () => GetStashDiffItems().Any()),
            Menu.SubMenu("Stash Drop", "", GetStashDropItems(), () => GetStashDropItems().Any())
        );
    }

    IEnumerable<MenuItem> GetStashPopItems()
    {
        return repo.Repo.Stashes.Select(s =>
            Menu.Item($"{s.Message}", "", () => cmds.StashPop(s.Name)));
    }

    IEnumerable<MenuItem> GetStashDropItems()
    {
        return repo.Repo.Stashes.Select(s =>
            Menu.Item($"{s.Message}", "", () => cmds.StashDrop(s.Name)));
    }

    IEnumerable<MenuItem> GetStashDiffItems()
    {
        return repo.Repo.Stashes.Select(s =>
            Menu.Item($"{s.Message}", "", () => cmds.StashDiff(s.Name)));
    }
    IEnumerable<MenuItem> GetConfigItems()
    {
        var path = repo.RepoPath;
        var previewTxt = config.Get().AllowPreview ? "Disable Preview Releases" : "Enable Preview Releases";
        var metaSyncTxt = repoConfig.Get(path).SyncMetaData ? "Disable this Repo Sync Metadata" : "Enable this Repo Sync Metadata";

        return EnumerableEx.From(
             Menu.Item(previewTxt, "", () =>
             {
                 config.Set(c => c.AllowPreview = !c.AllowPreview);
                 updater.CheckUpdateAvailableAsync().RunInBackground();
             }),
             Menu.Item(metaSyncTxt, "", () => repoConfig.Set(path, c => c.SyncMetaData = !c.SyncMetaData))
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
            items = items.Append(Menu.Item("Undo Set Branch", "", () => cmds.UndoSetBranch(commit.Id)));
        }

        var branch = repo.Branch(commit.BranchName);
        return items
            .Concat(branch.AmbiguousBranchNames.Select(n => repo.AllBranchByName(n))
                .DistinctBy(b => b.CommonName)
                .Select(b => Menu.Item(ToBranchMenuName(b), "", () => cmds.ResolveAmbiguity(branch, b.DisplayName))));
    }


    IEnumerable<MenuItem> GetSetBranchItems()
    {
        var items = Enumerable.Empty<MenuItem>();
        var commit = repo.RowCommit;

        if (commit.ChildIds.Count() > 1)
        {
            items = items.Append(Menu.Item("Set Branch ...", "", () => cmds.SetBranchManuallyAsync()));
        }
        if (commit.IsBranchSetByUser)
        {
            items = items.Append(Menu.Item("Undo Set Branch", "", () => cmds.UndoSetBranch(commit.Id)));
        }

        return items;
    }


    IEnumerable<MenuItem> GetPushItems()
    {
        var items = new List<MenuItem>();

        items.Add(Menu.Item("Push All Branches", "P", () => cmds.PushAllBranches()));

        if (repo.CurrentBranch != null)
        {
            items.Add(Menu.Item(ToBranchMenuName(repo.CurrentBranch), "p",
                    () => cmds.PushCurrentBranch(),
                    () => cmds.CanPushCurrentBranch()));
        }
        items.AddRange(repo.Branches
            .Where(b => !b.IsRemote && !b.IsCurrent && b.HasLocalOnly && !b.HasRemoteOnly)
            .Select(b => (Menu.Item($"Push {ToBranchMenuName(b)}", "", () => cmds.PushBranch(b.Name)))));

        if (repo.CurrentBranch != null && repo.CurrentBranch.RemoteName == "")
        {
            items.Add(Menu.Item($"Publish {repo.CurrentBranch.DisplayName}", "",
            () => cmds.PublishCurrentBranch()));
        }

        return items.DistinctBy(b => b.Title);
    }


    IEnumerable<MenuItem> GetOpenRepoItems()
    {
        return Menu.Items()
            .Add(GetRecentRepoItems())
            .Separator()
            .Item("Browse ...", "", () => cmds.ShowBrowseDialog())
            .Item("Clone ...", "", () => cmds.Clone(), () => true);
    }


    IEnumerable<MenuItem> GetRecentRepoItems() =>
        states.Get().RecentFolders.Where(Files.DirExists).Select(path => Menu.Item(path, "", () => cmds.ShowRepo(path)));


    IEnumerable<MenuItem> GetPullItems()
    {
        var items = new List<MenuItem>();
        items.Add(Menu.Item("Update/Pull All Branches", "U", () => cmds.PullAllBranches()));
        if (repo.CurrentBranch != null)
        {
            items.Add(Menu.Item(ToBranchMenuName(repo.CurrentBranch), "u",
                    () => cmds.PullCurrentBranch(),
                    () => cmds.CanPullCurrentBranch()));
        }
        items.AddRange(repo.Branches
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
             .Where(b => b.CommonName != currentName && b.LocalName == "" && b.PullMergeBranchName == "")
             .OrderBy(b => b.CommonName);

        return ToSwitchBranchesItems(branches);
    }

    IEnumerable<MenuItem> GetDeleteItems()
    {
        var branches = repo.GetAllBranches()
            .Where(b => b.IsGitBranch && !b.IsMainBranch && !b.IsCurrent && !b.IsLocalCurrent
                && b.LocalName == "" && b.PullMergeBranchName == "")
            .OrderBy(b => b.CommonName);

        return ToDeleteHiarchicalBranchesItems(branches);
    }


    IEnumerable<MenuItem> ToDeleteItems(IEnumerable<Branch> branches)
    {
        return branches.Select(b =>
                Menu.Item($"{ToBranchMenuName(b)} ...", "", () => cmds.DeleteBranch(b.Name)));
    }

    IEnumerable<MenuItem> ToDeleteHiarchicalBranchesItems(IEnumerable<Branch> branches)
    {
        if (branches.Count() <= HierachiacalCount)
        {   // Too few branches to bother with submenus
            return ToDeleteItems(branches);
        }

        var cic = repo.RowCommit;

        // Group by first part of the b.commonName (if '/' exists in name)
        var groups = branches
            .GroupBy(b => b.CommonName.Split('/')[0])
            .OrderBy(g => g.Key)
            .OrderBy(g => g.Count() > 1 ? 0 : 1);  // Sort groups first;

        // If only one item in group, then just show branch, otherwise show submenu
        return ToMaxBranchesItems(groups.Select(g =>
            g.Count() == 1
                ? Menu.Item($"{ToBranchMenuName(g.First())} ...", "", () => cmds.DeleteBranch(g.First().Name))
                : Menu.SubMenu($"    {g.Key}/┅", "", ToDeleteItems(g))));
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
             .Where(b => b.CommonName != currentName && b.LocalName == "" && b.PullMergeBranchName == "")
             .DistinctBy(b => b.TipId)
             .OrderBy(b => b.CommonName);

        // Include commit if not on current branch
        var commitItems = repo.Branch(commit.BranchName) != repo.CurrentBranch
            ? new[] { Menu.Item($"Commit {commit.Sid}", "", () => cmds.MergeBranch(commit.Id)) }
            : Enumerable.Empty<MenuItem>();

        // Incluce cherry pic if not on current branch
        var cherryPicItems = repo.RowCommit.Id != repo.CurrentBranch?.TipId
            ? new[] { Menu.Item($"Cherry Pic {sidText}", "", () => cmds.CherryPic(commit.Id), () => repo.Status.IsOk) }
            : Enumerable.Empty<MenuItem>();

        var items = branches
            .Select(b => Menu.Item(ToBranchMenuName(b), "", () => cmds.MergeBranch(b.Name)))
            .Concat(commitItems)
            .Concat(cherryPicItems);

        return items;
    }

    IEnumerable<MenuItem> GetPreviewMergeItems(bool isFromCurrentCommit, bool isSwitch)
    {
        var commit = repo.RowCommit;
        var currentName = repo.CurrentBranch?.CommonName ?? "";
        var branches = repo.Branches
             .Where(b => b.CommonName != currentName &&
                b.RemoteName == "" && b.PullMergeBranchName == "")
             .DistinctBy(b => b.DisplayName)
             .OrderBy(b => b.DisplayName);

        return branches.Select(b => Menu.Item(ToBranchMenuName(b, false, false, false), "", () => cmds.DiffWithOtherBranch(b.Name, isFromCurrentCommit, isSwitch)));
    }

    IEnumerable<MenuItem> GetHideItems()
    {
        var mainBranch = repo.Branches.First(b => b.IsMainBranch);
        var branches = repo.Branches
            .Where(b => !b.IsMainBranch && !b.IsDetached && b.CommonName != mainBranch.CommonName &&
                b.RemoteName == "" && b.PullMergeBranchName == "")
            .DistinctBy(b => b.DisplayName)
            .OrderBy(b => b.DisplayName);

        return ToHideHiarchicalBranchesItems(branches);
    }


    IEnumerable<MenuItem> GetShowBranchItems()
    {
        var allBranches = repo.GetAllBranches();

        var liveBranches = allBranches
            .Where(b => b.IsGitBranch)
            .OrderBy(b => b.DisplayName);

        var liveAndDeletedBranches = allBranches
            .OrderBy(b => b.DisplayName);

        var recentBranches = liveAndDeletedBranches
            .OrderBy(b => repo.Repo.AugmentedRepo.CommitById[b.TipId].GitIndex)
            .Take(RecentCount);

        var ambiguousBranches = allBranches
            .Where(b => b.AmbiguousTipId != "")
            .OrderBy(b => b.DisplayName);

        var items = EnumerableEx.From(
            Menu.SubMenu("Recent", "", ToShowHiarchicalBranchesItems(recentBranches)),
            Menu.SubMenu("All Live", "", ToShowHiarchicalBranchesItems(liveBranches)),
            Menu.SubMenu("All Live and Deleted", "", ToShowHiarchicalBranchesItems(liveAndDeletedBranches))
        );

        return ambiguousBranches.Any()
            ? items.Append(Menu.SubMenu("Ambiguous", "", ToShowBranchesItems(ambiguousBranches, false, true)))
            : items;
    }


    IEnumerable<MenuItem> GetUndoItems()
    {
        string id = repo.RowCommit.Id;
        string sid = repo.RowCommit.Sid;

        return Menu.Items()
            .Menu("Undo/Restore Uncommitted File", "", GetUncommittedFileItems(), () => cmds.CanUndoUncommitted())
            .Item($"Undo Commit {sid}", "", () => cmds.UndoCommit(id), () => cmds.CanUndoCommit())
            .Item($"Uncommit Last Commit", "", () => cmds.UncommitLastCommit(), () => cmds.CanUncommitLastCommit())
            .Separator()
            .Item("Undo/Restore all Uncommitted Changes", "",
                () => cmds.UndoAllUncommittedChanged(), () => cmds.CanUndoUncommitted())
            .Item("Clean/Restore Working Folder", "", () => cmds.CleanWorkingFolder());
    }

    IEnumerable<MenuItem> GetUncommittedFileItems() =>
        repo.GetUncommittedFiles().Select(f => Menu.Item(f, "", () => cmds.UndoUncommittedFile(f)));

    IEnumerable<MenuItem> ToHideBranchesItems(IEnumerable<Branch> branches)
    {
        return branches.Select(b =>
                Menu.Item(ToBranchMenuName(b, false, false, false), "", () => cmds.HideBranch(b.Name)));
    }


    IEnumerable<MenuItem> ToHideHiarchicalBranchesItems(IEnumerable<Branch> branches)
    {
        if (branches.Count() <= HierachiacalCount)
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
        return ToMaxBranchesItems(groups.Select(g =>
            g.Count() == 1
                ? Menu.Item(ToBranchMenuName(g.First(), cic, false), "", () => cmds.HideBranch(g.First().Name))
                : Menu.SubMenu($"    {g.Key}/┅", "", ToHideBranchesItems(g))));
    }

    IEnumerable<MenuItem> ToShowHiarchicalBranchesItems(IEnumerable<Branch> branches)
    {
        if (branches.Count() <= HierachiacalCount)
        {   // Too few branches to bother with submenus
            return ToShowBranchesItems(branches, false, false);
        }

        var cic = repo.RowCommit;

        // Group by first part of the b.commonName (if '/' exists in name)
        var groups = branches
            .GroupBy(b => b.CommonName.Split('/')[0])
            .OrderBy(g => g.Key)
            .OrderBy(g => g.Count() > 1 ? 0 : 1);  // Sort groups first;

        // If only one item in group, then just show branch, otherwise show submenu
        return ToMaxBranchesItems(groups.Select(g =>
            g.Count() == 1
                ? Menu.Item(ToBranchMenuName(g.First(), cic, false), "", () => cmds.ShowBranch(g.First().Name, false))
                : Menu.SubMenu($"    {g.Key}/┅", "", ToShowBranchesItems(g, false, false))));
    }

    IEnumerable<MenuItem> ToShowBranchesItems(
        IEnumerable<Branch> branches, bool canBeOutside = false, bool includeAmbiguous = false)
    {
        var cic = repo.RowCommit;
        return ToMaxBranchesItems(
            branches
            .Where(b => b.RemoteName == "" && b.PullMergeBranchName == "") // skip local pull merge
            .Select(b => Menu.Item(ToBranchMenuName(b, cic, canBeOutside), "",
                () => cmds.ShowBranch(b.Name, includeAmbiguous))));
    }


    IEnumerable<MenuItem> ToMaxBranchesItems(IEnumerable<MenuItem> items)
    {
        if (items.Count() <= HierachiacalCount)
        {   // Too few branches to bother with submenus
            return items;
        }

        return items.Take(HierachiacalCount)
            .Concat(new[] { Menu.SubMenu("More ...", "", ToMaxBranchesItems(items.Skip(RecentCount))) });
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
        string name = branch.DisplayName;

        name = branch.IsGitBranch ? " " + branch.DisplayName : "~" + name;
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
