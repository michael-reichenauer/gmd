using gmd.Common;
using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui;


interface IRepoViewMenus
{
    void ShowRepoMenu(int x, int y);
    void ShowCommitMenu(int x, int y, int index);
    void ShowBranchMenu(int x, int y, string branchName);
    void ShowCommitBranchesMenu(int x, int y);
    void ShowOpenBranchesMenu(int x = Menu.Center, int y = 0);
    void ShowMergeFromMenu(int x = Menu.Center, int y = 0);
    void ShowOpenRepoMenu(int x = Menu.Center, int y = 0);
    void ShowDiffBranchToMenu(int x, int y, string branchName);
}

class RepoViewMenus : IRepoViewMenus
{
    const int RecentCount = 15;
    const int MaxItemCount = 20;

    readonly IRepo repo;
    readonly IRepoCommands cmds;
    readonly IState states;
    private readonly IConfigDlg configDlg;

    internal RepoViewMenus(IRepo repo, IState states, IConfigDlg configDlg)
    {
        this.repo = repo;
        this.cmds = repo.Cmd;
        this.states = states;
        this.configDlg = configDlg;
    }

    public void ShowRepoMenu(int x, int y)
    {
        Menu.Show($"Repo Menu", x, y + 2, GetRepoMenuItems());
    }

    public void ShowCommitMenu(int x, int y, int index)
    {
        var c = repo.Commits[index];
        Menu.Show($"Commit: {Sid(c.Id)}", x, y + 2, GetCommitMenuItems(c.Id));
    }

    public void ShowBranchMenu(int x, int y, string branchName)
    {
        var b = repo.Branch(branchName);
        Menu.Show($"Branch: {b.ShortNiceUniqueName()}", x, y + 2, GetBranchMenuItems(branchName));
    }

    public void ShowCommitBranchesMenu(int x, int y)
    {
        Menu.Show("Show/Open Branch", x, y + 2, GetCommitBranchItems());
    }

    public void ShowMergeFromMenu(int x = Menu.Center, int y = 0)
    {
        Menu.Show("Merge from", x, y, GetMergeFromItems());
    }

    public void ShowOpenRepoMenu(int x = Menu.Center, int y = 0)
    {
        Menu.Show("Open Repo", x, y + 2, GetOpenRepoItems());
    }

    public void ShowOpenBranchesMenu(int x = Menu.Center, int y = 0)
    {
        Menu.Show("Open Branch", x, y + 2, GetShowBranchItems());
    }

    public void ShowDiffBranchToMenu(int x, int y, string branchName)
    {
        Menu.Show($"Diff Branch to {branchName}", x, y + 2, GetBranchDiffItems(branchName));
    }

    IEnumerable<MenuItem> GetRepoMenuItems()
    {
        var isStatusOK = repo.Status.IsOk;

        return Menu.Items
            .Item("Pull/Update All Branches", "Shift-U", () => cmds.PullAllBranches(), () => isStatusOK)
            .Item("Push All Branches", "Shift-P", () => cmds.PushAllBranches(), () => isStatusOK)
            .SubMenu("Stash", "", GetStashMenuItems())
            .Item("Search/Filter ...", "F", () => cmds.Filter())
            .Item("Refresh/Reload", "R", () => cmds.RefreshAndFetch())
            .Item("Clean/Restore Working Folder", "", () => cmds.CleanWorkingFolder())
            .SubMenu("Open/Clone Repo", "O", GetOpenRepoItems())
            .Item("Config ...", "", () => configDlg.Show(repo.RepoPath))
            .Item("Help ...", "?, F1", () => cmds.ShowHelp())
            .Item("About ...", "", () => cmds.ShowAbout())
            .Item("Quit", "Q, Esc", () => UI.Shutdown());
    }

    IEnumerable<MenuItem> GetCommitMenuItems(string commitId)
    {
        var c = repo.Commit(commitId);
        var cc = repo.GetCurrentCommit();
        var rb = repo.RowBranch;
        var cb = repo.CurrentBranch;
        var isStatusOK = repo.Status.IsOk;
        var sid = Sid(c.Id);

        return Menu.Items
            .Items(GetNewReleaseItems())
            .Item("Commit ...", "C", () => cmds.CommitFromMenu(false), () => !isStatusOK)
            .Item("Amend ...", "A", () => cmds.CommitFromMenu(true), () => !isStatusOK && cc.IsAhead)
            .Item("Commit Diff ...", "D", () => cmds.ShowDiff(c.Id))
            .SubMenu("Undo", "", GetCommitUndoItems())
            .Item("Stash Changes", "", () => cmds.Stash(), () => c.Id == Repo.UncommittedId)
            .SubMenu("Tag", "", GetTagItems(), () => c.Id != Repo.UncommittedId)
            .Item("Create Branch from Commit ...", "", () => cmds.CreateBranchFromCommit(), () => !c.IsUncommitted)
            .Item($"Merge From Commit to {cb?.ShortNiceUniqueName()}", "", () => cmds.MergeBranch(c.Id), () => isStatusOK && rb != cb)
            .Item($"Cherry Pick Commit to {cb?.ShortNiceUniqueName()}", "", () => cmds.CherryPick(c.Id), () => isStatusOK && rb != cb)
            .Item("Switch to Commit", "", () => cmds.SwitchToCommit(),
                    () => isStatusOK && repo.RowCommit.Id != repo.GetCurrentCommit().Id)
            .Separator()
            .SubMenu("Show/Open Branch", "Shift →", GetShowBranchItems())
            .Item("Toggle Commit Details ...", "Enter", () => cmds.ToggleDetails())
            .Item("File History ...", "", () => cmds.ShowFileHistory())
            .SubMenu("Repo Menu", "", GetRepoMenuItems());
    }


    IEnumerable<MenuItem> GetBranchMenuItems(string name)
    {
        var c = repo.RowCommit;
        var b = repo.Branch(name);
        var cb = repo.CurrentBranch;
        var isStatusOK = repo.Status.IsOk;
        var isCurrent = b.IsCurrent || b.IsLocalCurrent;
        var mergeToName = cb?.ShortNiceUniqueName() ?? "";

        return Menu.Items
            .Items(GetNewReleaseItems())
            .Item(GetSwitchToBranchItem(name))
            .Item(!isCurrent, $"Merge to {mergeToName}", "E", () => cmds.MergeBranch(b.Name), () => !b.IsCurrent && !b.IsLocalCurrent && isStatusOK)
            .SubMenu(isCurrent, "Merge from", "E", GetMergeFromItems())
            .Item("Hide Branch", "H", () => cmds.HideBranch(name))
            .Item("Pull/Update", "U", () => cmds.PullBranch(name), () => b.HasRemoteOnly && isStatusOK)
            .Item("Push", "P", () => cmds.PushBranch(name), () => (b.HasLocalOnly || (!b.IsRemote && b.PullMergeParentBranchName == "")) && isStatusOK)
            .Item("Create Branch ...", "B", () => cmds.CreateBranchFromBranch(b.Name))
            .Item("Delete Branch ...", "", () => cmds.DeleteBranch(b.Name), () => b.IsGitBranch && !b.IsMainBranch && !b.IsCurrent && !b.IsLocalCurrent)
            .SubMenu("Diff Branch to", "D", GetBranchDiffItems(name))
            .Item("Change Branch Color", "G", () => cmds.ChangeBranchColor(), () => !repo.Branch(repo.RowCommit.BranchName).IsMainBranch)
            .Items(GetMoveBranchItems(name))
            .Separator()
            .SubMenu("Show/Open Branch", "Shift →", GetShowBranchItems())
            .Item("Hide All Branches", "", () => cmds.HideBranch("", true))
            .Item("Pull/Update All Branches", "Shift-U", () => cmds.PullAllBranches(), () => isStatusOK)
            .Item("Push All Branches", "Shift-P", () => cmds.PushAllBranches(), () => isStatusOK)
            .Item("Set Commit Branch Manually ...", "", () => cmds.SetBranchManuallyAsync(), () => !c.IsUncommitted)
            .SubMenu("Repo Menu", "", GetRepoMenuItems());
    }


    IEnumerable<MenuItem> GetNewReleaseItems()
    {
        if (!states.Get().Releases.IsUpdateAvailable()) return Menu.Items;
        return Menu.Items
           .Separator("New Release Available !!!")
           .Item("Update to Latest Version ...", "", () => cmds.UpdateRelease())
           .Separator();
    }


    IEnumerable<MenuItem> GetTagItems() => Menu.Items
        .Item("Add Tag ...", "T", () => cmds.AddTag(), () => !repo.RowCommit.IsUncommitted)
        .SubMenu("Remove Tag", "", GetDeleteTagItems());


    IEnumerable<MenuItem> GetDeleteTagItems()
    {
        var commit = repo.RowCommit;
        return commit.Tags.Select(t => Menu.Item(t.Name, "", () => cmds.DeleteTag(t.Name)));
    }


    IEnumerable<MenuItem> GetMoveBranchItems(string branchPrimaryName)
    {
        // Get possible local, remote, pull merge branches of the row branch
        var relatedBranches = repo.Branches.Where(b => b.PrimaryName == branchPrimaryName);
        var branch = repo.Branch(branchPrimaryName);

        // Get all branches that overlap with any of the related branches
        var overlappingBranches = relatedBranches
            .SelectMany(b => repo.Graph.GetOverlappingBranches(b.Name))
            .Distinct()
            .ToList();

        if (!overlappingBranches.Any()) return Menu.Items;

        // Sort on left to right shown order
        Sorter.Sort(overlappingBranches, (b1, b2) => b1.X < b2.X ? -1 : b1.X > b2.X ? 1 : 0);

        // Find possible branch on left side to move to before (skip if ancestor)
        Branch? leftBranch = null;
        for (int i = 0; i < overlappingBranches.Count; i++)
        {
            var b = overlappingBranches[i];
            if (b.B.PrimaryName == branchPrimaryName) break;
            leftBranch = b.B;
        }
        leftBranch = leftBranch != null ? repo.Branch(leftBranch.PrimaryName) : null;
        var leftPrimaryName = leftBranch != null && !IsAncestor(leftBranch, branch) ? leftBranch.PrimaryName : "";

        // Find possible branch on right side to move to after (skip if ancestor)
        Branch? rightBranch = null;
        for (int i = overlappingBranches.Count - 1; i >= 0; i--)
        {
            var b = overlappingBranches[i];
            if (b.B.PrimaryName == branchPrimaryName) break;
            rightBranch = b.B;
        }
        rightBranch = rightBranch != null ? repo.Branch(rightBranch.PrimaryName) : null;
        var rightPrimaryName = rightBranch != null && !IsAncestor(branch, rightBranch) ? rightBranch.PrimaryName : "";

        var items = Menu.Items;
        // Add menu items if movable branches found
        if (leftPrimaryName != "")
        {
            items.Item($"<= (Move Branch left of {leftBranch!.NiceNameUnique})", "",
                () => cmds.MoveBranch(branch.PrimaryName, leftPrimaryName, -1));
        }
        if (rightPrimaryName != "")
        {
            items.Item($"=> (Move right of {rightBranch!.NiceNameUnique})", "",
                () => cmds.MoveBranch(branch.PrimaryName, rightPrimaryName, +1));
        }

        return items;
    }

    IEnumerable<MenuItem> GetMergeFromItems()
    {
        if (!repo.Status.IsOk) return Menu.Items;

        var currentName = repo.CurrentBranch?.PrimaryName ?? "";

        // Get all branches except current
        var branches = repo.Branches
             .Where(b => b.IsPrimary && b.PrimaryName != currentName)
             .DistinctBy(b => b.TipId)
             .OrderBy(b => b.PrimaryName);

        return branches.Select(b => Menu.Item(ToBranchMenuName(b), "", () => cmds.MergeBranch(b.Name)));
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


    IEnumerable<MenuItem> GetOpenRepoItems() => Menu.Items
        .Items(GetRecentRepoItems())
        .Separator()
        .Item("Browse ...", "", () => cmds.ShowBrowseDialog())
        .Item("Clone ...", "", () => cmds.Clone(), () => true);


    IEnumerable<MenuItem> GetRecentRepoItems() =>
        states.Get().RecentFolders
            .Where(Directory.Exists)
            .Take(10)
            .Select(path => Menu.Item(path, "", () => cmds.ShowRepo(path), () => path != repo.RepoPath));

    IEnumerable<MenuItem> GetCommitInOutItems()
    {
        // Get current branch, commit branch in/out and all shown branches
        var branches = repo.GetCommitBranches().Concat(repo.Branches);

        var currentBranch = repo.GetCurrentBranch();
        if (currentBranch != null && !branches.ContainsBy(b => b.PrimaryName == currentBranch.PrimaryName))
        {
            branches = branches.Prepend(currentBranch);
        }
        branches = branches.Where(b => !repo.Branches.ContainsBy(bb => bb.PrimaryName == b.PrimaryName));

        return ToBranchesItems(branches, b => cmds.ShowBranch(b.Name, false), null, true);
    }

    IEnumerable<MenuItem> GetCommitBranchItems()
    {
        // Get commit branch in/out
        var branches = repo.GetCommitBranches();

        return ToBranchesItems(branches, b => cmds.ShowBranch(b.Name, false), null, true);
    }

    MenuItem GetSwitchToBranchItem(string branchName)
    {
        var currentName = repo.CurrentBranch?.PrimaryName ?? "";
        var branch = repo.Branch(branchName);
        if (branch.LocalName != "") branchName = branch.LocalName;
        return Menu.Item("Switch to Branch", "S", () => cmds.SwitchTo(branchName), () => branch.PrimaryName != currentName);
    }


    IEnumerable<MenuItem> GetBranchDiffItems(string branchName)
    {
        if (!repo.Status.IsOk) return Menu.Items;

        var primaryName = repo.Branch(branchName).PrimaryName;
        var branches = repo.Branches
             .Where(b => b.IsPrimary && b.PrimaryName != primaryName)
             .DistinctBy(b => b.NiceNameUnique)
             .OrderBy(b => b.NiceNameUnique);

        return branches.Select(b => Menu.Item(ToBranchMenuName(b), "", () => cmds.DiffBranchesBranch(branchName, b.Name)));
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
            .Items(GetCommitInOutItems())
            .SubMenu("    Recent", "", ToBranchesItems(recentBranches, ShowBranch)
                .Prepend(Menu.Item("Show 5 more Recent", "", () => cmds.ShowBranch("", false, ShowBranches.AllRecent, 5))))
            .SubMenu("    Active", "", ToHierarchicalBranchesItems(liveBranches, ShowBranch)
                .Prepend(Menu.Item("Show All Active", "", () => cmds.ShowBranch("", false, ShowBranches.AllActive))))
            .SubMenu("    Active and Deleted", "", ToHierarchicalBranchesItems(liveAndDeletedBranches, ShowBranch)
                .Prepend(Menu.Item("Show All Active and Deleted", "", () => cmds.ShowBranch("", false, ShowBranches.AllActiveAndDeleted))));

        return ambiguousBranches.Any()
            ? items.SubMenu("    Ambiguous", "", ToBranchesItems(ambiguousBranches, b => cmds.ShowBranch(b.Name, true)))
            : items;
    }

    void ShowBranch(Branch b) => cmds.ShowBranch(b.Name, false);

    IEnumerable<MenuItem> GetCommitUndoItems()
    {
        string id = repo.RowCommit.Id;
        var binaryPaths = repo.Status.AddedFiles
            .Concat(repo.Status.ModifiedFiles)
            .Concat(repo.Status.RenamedTargetFiles)
            .Where(f => !Files.IsText(Path.Join(repo.RepoPath, f)))
            .ToList();

        return Menu.Items
            .SubMenu("Undo/Restore an Uncommitted File", "", GetUncommittedFileItems(), () => cmds.CanUndoUncommitted())
            .Item($"Undo Commit", "", () => cmds.UndoCommit(id), () => repo.Status.IsOk)
            .Item($"Uncommit", "", () => cmds.UncommitLastCommit(), () => cmds.CanUncommitLastCommit())
            .Separator()
            .Item("Undo/Restore all Uncommitted Binary Files", "", () => cmds.UndoUncommittedFiles(binaryPaths), () => binaryPaths.Any())
            .Item("Undo/Restore all Uncommitted Changes", "",
                () => cmds.UndoAllUncommittedChanged(), () => cmds.CanUndoUncommitted());
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
        string ToGroupName(IGrouping<string, Branch> bs) => bs.All(b => b.NiceName == bs.First().NiceName)
                    ? $"    {bs.Key}(*)" : $"    {bs.Key}/*";
        return groups.Select(g =>
            g.Count() == 1
                ? ToBranchesItems(g, action, canExecute, false, isNoShowIcon).First()
                : Menu.SubMenu(ToGroupName(g), "", ToHierarchicalBranchesItemsImpl(g, action, canExecute, isNoShowIcon, level + 1)));
    }


    IEnumerable<MenuItem> ToBranchesItems(
        IEnumerable<Branch> branches,
        Action<Branch> action,
        Func<Branch, bool>? canExecute = null,
        bool canBeOutside = false,
        bool isNoShowIcon = false)
    {
        canExecute ??= (b => true);
        return branches.Select(b => Menu.Item(ToBranchMenuName(b, canBeOutside, isNoShowIcon), b.IsCurrent || b.IsLocalCurrent ? "Y" : "",
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
        name = isShown ? "o" + name : " " + name;
        name = branch.IsCurrent || branch.IsLocalCurrent ? "●" + name : " " + name;

        return name;
    }

    static bool IsAncestor(Branch b1, Branch? b2)
    {
        if (b2 == null) return false;
        return b2.AncestorNames.Contains(b1.Name);
    }

    static string Sid(string id) => id == Repo.UncommittedId ? "uncommitted" : id.Sid();
}
