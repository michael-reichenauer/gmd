using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.RepoView;


interface IBranchMenu
{
    void Show(int x, int y, string branchName);
    void ShowOpenBranchMenu(int x = Menu.Center, int y = 0);
    void ShowDiffBranchToMenu(int x, int y, string branchName);
    void ShowCommitBranchesMenu(int x, int y);
    void ShowMergeFromMenu(int x = Menu.Center, int y = 0);

    IEnumerable<MenuItem> GetBranchMenuItems(string branchName, bool isLimited = false);
    IEnumerable<MenuItem> GetShowBranchItems();
}


class BranchMenu : IBranchMenu
{
    const int RecentCount = 15;
    const int MaxItemCount = 20;

    readonly IRepoMenu repoMenu;
    readonly IViewRepo repo;
    readonly IBranchCommands cmds;

    public BranchMenu(IRepoMenu repoMenu, IViewRepo repo)
    {
        this.repoMenu = repoMenu;
        this.repo = repo;
        this.cmds = repo.BranchCmds;
    }

    public void Show(int x, int y, string branchName)
    {
        var b = repo.Repo.BranchByName[branchName];
        Menu.Show($"Branch: {b.ShortNiceUniqueName()}", x, y + 2, GetBranchMenuItems(branchName));
    }

    public void ShowOpenBranchMenu(int x = Menu.Center, int y = 0)
    {
        Menu.Show("Open Branch", x, y + 2, GetShowBranchItems());
    }

    public void ShowDiffBranchToMenu(int x, int y, string branchName)
    {
        Menu.Show($"Diff Branch to {branchName}", x, y + 2, GetBranchDiffItems(branchName));
    }

    public void ShowCommitBranchesMenu(int x, int y)
    {
        Menu.Show("Show/Hide Branch", x, y + 2, GetCommitBranchItems());
    }

    public void ShowMergeFromMenu(int x = Menu.Center, int y = 0)
    {
        Menu.Show("Merge from", x, y, GetMergeFromItems());
    }


    public IEnumerable<MenuItem> GetBranchMenuItems(string branchName, bool isLimited = false)
    {
        var c = repo.RowCommit;
        var b = repo.Repo.BranchByName[branchName];
        var cb = repo.Repo.CurrentBranch();
        var isStatusOK = repo.Repo.Status.IsOk;
        var isCurrent = b.IsCurrent || b.IsLocalCurrent;
        var mergeToName = cb?.ShortNiceUniqueName() ?? "";

        return Menu.Items
            .Items(repoMenu.GetNewReleaseItems())
            .Item(GetSwitchToBranchItem(branchName))
            .Item(!isCurrent, $"Merge to {mergeToName}", "E", () => cmds.MergeBranch(b.Name), () => !b.IsCurrent && !b.IsLocalCurrent && isStatusOK)
            .SubMenu(isCurrent, "Merge from", "E", GetMergeFromItems())
            .SubMenu("Rebase and push on", "", GetRebaseFromItems(b))
            .Item("Hide Branch", "H", () => cmds.HideBranch(branchName))
            .Item("Pull/Update", "U", () => cmds.PullBranch(branchName), () => b.HasRemoteOnly && isStatusOK)
            .Item("Push", "P", () => cmds.PushBranch(branchName), () => (b.HasLocalOnly || (!b.IsRemote && b.PullMergeParentBranchName == "")) && isStatusOK)
            .Item("Create Branch ...", "B", () => cmds.CreateBranchFromBranch(b.Name))
            .Item("Delete Branch ...", "", () => cmds.DeleteBranch(b.Name), () => b.IsGitBranch && !b.IsMainBranch && !b.IsCurrent && !b.IsLocalCurrent)
            .SubMenu("Diff Branch to", "D", GetBranchDiffItems(branchName))
            .Item("Change Branch Color", "G", () => cmds.ChangeBranchColor(branchName), () => !repo.Repo.BranchByName[branchName].IsMainBranch)
            .Items(GetMoveBranchItems(branchName))
            .Separator()
            .SubMenu(!isLimited, "Show/Open Branch", "Shift →", GetShowBranchItems())
            .Item("Pull/Update All Branches", "Shift-U", () => cmds.PullAllBranches())
            .Item("Push All Branches", "Shift-P", () => cmds.PushAllBranches(), () => isStatusOK)
            .Item("Set Commit Branch Manually ...", "", () => cmds.SetBranchManuallyAsync(), () => !c.IsUncommitted)
            .SubMenu(!isLimited, "Repo Menu", "", repoMenu.GetRepoMenuItems());
    }

    MenuItem GetSwitchToBranchItem(string branchName)
    {
        var currentName = repo.Repo.CurrentBranch().PrimaryName;
        var branch = repo.Repo.BranchByName[branchName];
        if (branch.LocalName != "") branchName = branch.LocalName;
        return Menu.Item("Switch/Checkout to Branch", "S", () => cmds.SwitchTo(branchName), () => branch.PrimaryName != currentName);
    }


    IEnumerable<MenuItem> GetMergeFromItems()
    {
        if (!repo.Repo.Status.IsOk) return Menu.Items;

        var currentName = repo.Repo.CurrentBranch().PrimaryName;

        // Get all branches except current
        var branches = repo.Repo.ViewBranches
             .Where(b => b.IsPrimary && b.PrimaryName != currentName)
             .DistinctBy(b => b.TipId)
             .OrderBy(b => b.PrimaryName);

        return branches.Select(b => Menu.Item(ToBranchMenuName(b), "", () => cmds.MergeBranch(b.Name)));
    }

    IEnumerable<MenuItem> GetRebaseFromItems(Branch selectedBranch)
    {
        var sb = selectedBranch;
        var isCurrent = sb.IsCurrent || sb.IsLocalCurrent;
        if (!repo.Repo.Status.IsOk || !isCurrent) return Menu.Items;

        var primaryBranch = repo.Repo.ViewBranches.First(b => b.Name == sb.PrimaryName);
        var parentBranch = repo.Repo.ViewBranches.FirstOrDefault(b => b.Name == primaryBranch.ParentBranchName);
        if (parentBranch == null) return Menu.Items;

        // Get all branches except current (with parent branch first)
        var branches = repo.Repo.ViewBranches
             .Where(b => b.IsPrimary && b.PrimaryName != sb.PrimaryName && b.PrimaryName != parentBranch.PrimaryName)
             .DistinctBy(b => b.TipId)
             .OrderBy(b => b.PrimaryName)
             .Prepend(parentBranch);

        return branches.Select(b => Menu.Item(ToBranchMenuName(b), "", () => cmds.RebaseBranchOnto(b.Name)));
    }


    IEnumerable<MenuItem> GetBranchDiffItems(string branchName)
    {
        if (!repo.Repo.Status.IsOk) return Menu.Items;

        var primaryName = repo.Repo.BranchByName[branchName].PrimaryName;
        var branches = repo.Repo.ViewBranches
             .Where(b => b.IsPrimary && b.PrimaryName != primaryName)
             .DistinctBy(b => b.NiceNameUnique)
             .OrderBy(b => b.NiceNameUnique);

        return branches.Select(b => Menu.Item(ToBranchMenuName(b), "", () => cmds.DiffBranchesBranch(branchName, b.Name)));
    }


    IEnumerable<MenuItem> GetMoveBranchItems(string branchPrimaryName)
    {
        // Get possible local, remote, pull merge branches of the row branch
        var relatedBranches = repo.Repo.ViewBranches.Where(b => b.PrimaryName == branchPrimaryName);
        var branch = repo.Repo.BranchByName[branchPrimaryName];

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
        leftBranch = leftBranch != null ? repo.Repo.BranchByName[leftBranch.PrimaryName] : null;
        var leftPrimaryName = leftBranch != null && !IsAncestor(leftBranch, branch) ? leftBranch.PrimaryName : "";

        // Find possible branch on right side to move to after (skip if ancestor)
        Branch? rightBranch = null;
        for (int i = overlappingBranches.Count - 1; i >= 0; i--)
        {
            var b = overlappingBranches[i];
            if (b.B.PrimaryName == branchPrimaryName) break;
            rightBranch = b.B;
        }
        rightBranch = rightBranch != null ? repo.Repo.BranchByName[rightBranch.PrimaryName] : null;
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


    IEnumerable<MenuItem> GetCommitBranchItems()
    {
        // Get commit branch in/out
        var rowBranch = repo.RowBranch;
        var branches = repo.GetCommitBranches(true);
        var hiddenBranches = branches.Where(b => !b.IsInView).ToList();
        var shownBranches = branches.Where(b => b.IsInView && !rowBranch.AncestorNames.Contains(b.Name)).ToList();

        // Row branch is hidable if it is the tip of the row commit or if it is descendant of a shown branch
        bool isRowBranchHidable =
           branches.Any(b => b.IsInView && rowBranch.AncestorNames.Contains(b.Name));

        // Return hidden branches that can be shown, followed by shown branches that can be hidden
        return Menu.Items
            .Separator(hiddenBranches.Any(), "Show")
            .Items(ToBranchesItems(hiddenBranches, b => cmds.ShowBranch(b.Name, false), null, true))

            .Separator(shownBranches.Any() || isRowBranchHidable, "Hide")
            .Items(ToBranchesItems(shownBranches, b => cmds.HideBranch(b.Name, false)))
            .Items(isRowBranchHidable, ToBranchesItems(new[] { rowBranch }, b => cmds.HideBranch(b.Name, false)));
    }


    public IEnumerable<MenuItem> GetShowBranchItems()
    {
        var currentAuthor = repo.CurrentAuthor;
        var allBranches = repo.Repo.AllBranches;

        var liveBranches = allBranches
            .Where(b => b.IsGitBranch && b.IsPrimary)
            .OrderBy(b => b.NiceNameUnique);

        var myLiveBranches = allBranches
            .Where(b => b.IsGitBranch && b.IsPrimary && repo.Repo.CommitById[b.TipId].Author == currentAuthor)
            .OrderBy(b => b.NiceNameUnique);

        var liveAndDeletedBranches = allBranches
            .Where(b => b.IsPrimary)
            .OrderBy(b => b.NiceNameUnique)
            .ToList();

        var recentBranches = liveAndDeletedBranches
            .Where(b => b.IsPrimary)
            .OrderBy(b => repo.Repo.CommitById[b.TipId].GitIndex)
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
            .SubMenu("    My Active", "", ToHierarchicalBranchesItems(myLiveBranches, ShowBranch))
            .SubMenu("    Active and Deleted", "", ToHierarchicalBranchesItems(liveAndDeletedBranches, ShowBranch)
                .Prepend(Menu.Item("Show All Active and Deleted", "", () => cmds.ShowBranch("", false, ShowBranches.AllActiveAndDeleted))));

        return ambiguousBranches.Any()
            ? items.SubMenu("    Ambiguous", "", ToBranchesItems(ambiguousBranches, b => cmds.ShowBranch(b.Name, true)))
            : items;
    }

    void ShowBranch(Branch b) => cmds.ShowBranch(b.Name, false);


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
        return branches.Select(b => Menu.Item(ToBranchMenuName(b, canBeOutside, isNoShowIcon), ToBranchOwnerInitials(b),
            () => action(b), () => canExecute(b)));
    }

    string ToBranchOwnerInitials(Branch b)
    {
        var tip = repo.Repo.CommitById[b.TipId];
        var initials = string.Join(' ',
            tip.Author
                .Split(' ')
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .Take(2)
                .Select(p => p[0]));

        return $"'{initials}'";
    }


    string ToBranchMenuName(Branch branch, bool canBeOutside = false, bool isNoShowIcon = false)
    {
        var cic = repo.RowCommit;
        bool isBranchIn = false;
        bool isBranchOut = false;
        if (canBeOutside && !branch.IsInView)
        {   // The branch is currently not shown
            if (cic.ParentIds.Count > 1 &&
                repo.Repo.CommitById[cic.ParentIds[1]].BranchName == branch.Name)
            {   // Is a branch merge in '╮' branch                     
                isBranchIn = true;
            }
            else if (cic.AllChildIds.ContainsBy(id =>
                 repo.Repo.CommitById[id].BranchName == branch.Name))
            {   // Is branch out '╯' branch
                isBranchOut = true;
            }
        }

        var isShown = !isNoShowIcon && branch.IsInView;
        string name = branch.NiceNameUnique;

        name = branch.IsGitBranch ? " " + branch.NiceNameUnique : "~" + name;
        name = isBranchIn ? "╮" + name : name;
        name = isBranchOut ? "╯" + name : name;
        name = isBranchIn || isBranchOut ? name : " " + name;
        name = isShown ? "o" + name : " " + name;
        name = branch.IsCurrent || branch.IsLocalCurrent ? "●" + name : " " + name;

        return name;
    }


    IEnumerable<MenuItem> GetCommitInOutItems()
    {
        // Get current branch, commit branch in/out and all shown branches
        var branches = repo.GetCommitBranches(false).Concat(repo.Repo.ViewBranches);

        var currentBranch = repo.Repo.CurrentBranch();
        if (currentBranch != null && !branches.ContainsBy(b => b.PrimaryName == currentBranch.PrimaryName))
        {
            branches = branches.Prepend(currentBranch);
        }
        branches = branches.Where(b => !repo.Repo.ViewBranches.ContainsBy(bb => bb.PrimaryName == b.PrimaryName));

        return ToBranchesItems(branches, b => cmds.ShowBranch(b.Name, false), null, true);
    }

    static bool IsAncestor(Branch b1, Branch? b2)
    {
        if (b2 == null) return false;
        return b2.AncestorNames.Contains(b1.Name);
    }
}
