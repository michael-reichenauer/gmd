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
    void ShowMergeToMenu(int x = Menu.Center, int y = 0);

    IEnumerable<MenuItem> GetBranchMenuItems(string branchName, bool isLimited = false);
    IEnumerable<MenuItem> GetShowBranchItems();
    IEnumerable<MenuItem> GetShownBranchesItems();
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

    public void ShowMergeToMenu(int x = Menu.Center, int y = 0)
    {
        Menu.Show("Merge to", x, y, GetMergeToItems());
    }

    public IEnumerable<MenuItem> GetBranchMenuItems(string branchName, bool isLimited = false)
    {
        var c = repo.RowCommit;
        var b = repo.Repo.BranchByName[branchName];
        var cb = repo.Repo.CurrentBranch();
        var isStatusOK = repo.Repo.Status.IsOk;
        var isCurrent = b.IsCurrent || b.IsLocalCurrent;
        var currentName = cb.ShortNiceUniqueName();

        return Menu
            .Items.Items(repoMenu.GetNewReleaseItems())
            .Item(GetSwitchToBranchItem(branchName))
            // Both directions, worded from the branch this menu is for: 'to' merges it into the
            // named branch, 'from' merges the named branch into it. Merging into a branch that is
            // not current means checking it out on the way, so it is only offered for a git branch,
            // and not for one checked out in another worktree, which git refuses to check out.
            .Item(
                !isCurrent,
                $"Merge to {currentName}",
                "E",
                () => cmds.MergeBranch(b.Name),
                () => !b.IsCurrent && !b.IsLocalCurrent && isStatusOK
            )
            .Item(
                !isCurrent,
                $"Merge from {currentName}",
                "Shift-E",
                () => cmds.MergeToBranch(LocalName(b)),
                () => !b.IsCurrent && !b.IsLocalCurrent && isStatusOK && b.IsGitBranch && !IsInWorktree(b)
            )
            .SubMenu(isCurrent, "Merge from", "E", GetMergeFromItems())
            .SubMenu(isCurrent, "Merge to", "Shift-E", GetMergeToItems())
            .SubMenu("Rebase and push on", "", GetRebaseFromItems(b))
            .Item("Hide Branch", "H", () => cmds.HideBranch(branchName))
            // The current branch is pulled with 'git pull', which merges, so it can be pulled even
            // when diverged. Any other branch is updated with a fetch, which only fast-forwards,
            // so a diverged one can only be pulled by switching to it first. A branch checked out
            // in another worktree cannot be pulled from here at all, git refuses to move it.
            .Item(
                "Pull/Update",
                "U",
                () =>
                {
                    if (isCurrent)
                        cmds.PullCurrentBranch();
                    else
                        cmds.PullBranch(branchName);
                },
                () => b.HasRemoteOnly && isStatusOK && (isCurrent || !b.HasLocalOnly) && !IsInWorktree(b)
            )
            .Item(
                "Push",
                "P",
                () => cmds.PushBranch(branchName),
                () => (b.HasLocalOnly || (!b.IsRemote && b.PullMergeParentBranchName == "")) && isStatusOK
            )
            .Item("Create Branch ...", "B", () => cmds.CreateBranchFromBranch(b.Name))
            // A folder with this branch checked out, or with a new branch started from it when it
            // is checked out already (here or in another worktree)
            .Item("Create Worktree ...", "", () => cmds.CreateWorktree(b.Name), () => b.IsGitBranch)
            .Item(
                "Rename Branch ...",
                "",
                () => cmds.RenameBranch(b.Name),
                // The current branch can be renamed, git moves HEAD with it, but a branch git no
                // longer has cannot, the main branch is what the branch structure is based on, and
                // a remote branch without a local branch has no local branch to rename
                () => b.IsGitBranch && !b.IsMainBranch && !b.IsDetached && (!b.IsRemote || b.LocalName != "")
            )
            .Item(
                "Delete Branch ...",
                "",
                () => cmds.DeleteBranch(b.Name),
                // Nor a branch checked out in another worktree, which git refuses to delete
                () => b.IsGitBranch && !b.IsMainBranch && !b.IsCurrent && !b.IsLocalCurrent && !IsInWorktree(b)
            )
            .SubMenu("Diff Branch to", "D", GetBranchDiffItems(branchName))
            .Item(
                "Change Branch Color",
                "G",
                () => cmds.ChangeBranchColor(branchName),
                // Main is always magenta and a deleted branch always gray, so neither can be changed
                () => !repo.Repo.BranchByName[branchName].IsMainBranch && repo.Repo.BranchByName[branchName].IsGitBranch
            )
            .Items(GetMoveBranchItems(branchName))
            .Separator()
            // The limited menu is the one under a branch in the Branches sub menu of the commit menu,
            // which already offers these at its root, and the repo menu beside it
            .SubMenu(!isLimited, "Show/Open Branch", "Shift →", GetShowBranchItems())
            .Item(!isLimited, "Pull/Update All Branches", "Shift-U", () => cmds.PullAllBranches())
            .Item(!isLimited, "Push All Branches", "Shift-P", () => cmds.PushAllBranches(), () => isStatusOK)
            .Item("Set Commit Branch Manually ...", "", () => cmds.SetBranchManuallyAsync(), () => !c.IsUncommitted)
            .SubMenu(!isLimited, "Repo Menu", "", repoMenu.GetRepoMenuItems());
    }

    // A branch checked out in another worktree cannot be checked out here, git refuses, so the
    // same key opens that worktree instead — the command does the redirect, since the S key and
    // a double click reach it without this menu; the item only says what will happen
    MenuItem GetSwitchToBranchItem(string branchName)
    {
        var currentName = repo.Repo.CurrentBranch().PrimaryName;
        var branch = repo.Repo.BranchByName[branchName];
        if (branch.LocalName != "")
            branchName = branch.LocalName;

        var worktreePath = repo.Repo.WorktreePathOf(branch);
        if (worktreePath != "")
        {
            return Menu.Item($"Open Worktree {ShortPath(worktreePath)}", "S", () => cmds.SwitchTo(branchName));
        }

        return Menu.Item(
            "Switch/Checkout to Branch",
            "S",
            () => cmds.SwitchTo(branchName),
            () => branch.PrimaryName != currentName
        );
    }

    bool IsInWorktree(Branch branch) => repo.Repo.WorktreePathOf(branch) != "";

    // The end of a path, which is what tells worktrees apart; the start is the same for all
    static string ShortPath(string path) => path.Length <= 30 ? path : $"┅{path[^30..]}";

    IEnumerable<MenuItem> GetMergeFromItems() =>
        GetMergeBranches().Select(b => Menu.Item(ToBranchMenuName(b), "", () => cmds.MergeBranch(b.Name)));

    // The other direction: the current branch is merged into the picked one. The picked branch is
    // checked out on the way, so it has to be one git still has, else SwitchToAsync would recreate
    // it, and not one checked out in another worktree, and it is the local branch of a pair that
    // is named.
    IEnumerable<MenuItem> GetMergeToItems() =>
        GetMergeBranches()
            .Where(b => b.IsGitBranch && !IsInWorktree(b))
            .Select(b => Menu.Item(ToBranchMenuName(b), "", () => cmds.MergeToBranch(LocalName(b))));

    // The branches a merge can involve, i.e. all shown branches except the current one, which is
    // always the other end of the merge.
    IEnumerable<Branch> GetMergeBranches()
    {
        if (!repo.Repo.Status.IsOk)
            return [];

        var currentName = repo.Repo.CurrentBranch().PrimaryName;

        return repo
            .Repo.ViewBranches.Where(b => b.IsPrimary && b.PrimaryName != currentName)
            .DistinctBy(b => b.TipId)
            .OrderBy(b => b.PrimaryName);
    }

    static string LocalName(Branch branch) => branch.LocalName != "" ? branch.LocalName : branch.Name;

    IEnumerable<MenuItem> GetRebaseFromItems(Branch selectedBranch)
    {
        var sb = selectedBranch;
        var isCurrent = sb.IsCurrent || sb.IsLocalCurrent;
        if (!repo.Repo.Status.IsOk || !isCurrent)
            return Menu.Items;

        var primaryBranch = repo.Repo.ViewBranches.First(b => b.Name == sb.PrimaryName);
        var parentBranch = repo.Repo.ViewBranches.FirstOrDefault(b => b.Name == primaryBranch.ParentBranchName);
        if (parentBranch == null)
            return Menu.Items;

        // Get all branches except current (with parent branch first)
        var branches = repo
            .Repo.ViewBranches.Where(b =>
                b.IsPrimary && b.PrimaryName != sb.PrimaryName && b.PrimaryName != parentBranch.PrimaryName
            )
            .DistinctBy(b => b.TipId)
            .OrderBy(b => b.PrimaryName)
            .Prepend(parentBranch);

        return branches.Select(b => Menu.Item(ToBranchMenuName(b), "", () => cmds.RebaseBranchOnto(b.Name)));
    }

    IEnumerable<MenuItem> GetBranchDiffItems(string branchName)
    {
        if (!repo.Repo.Status.IsOk)
            return Menu.Items;

        var primaryName = repo.Repo.BranchByName[branchName].PrimaryName;
        var branches = repo
            .Repo.ViewBranches.Where(b => b.IsPrimary && b.PrimaryName != primaryName)
            .DistinctBy(b => b.NiceNameUnique)
            .OrderBy(b => b.NiceNameUnique);

        return branches.Select(b =>
            Menu.Item(ToBranchMenuName(b), "", () => cmds.DiffBranchesBranch(branchName, b.Name))
        );
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

        if (!overlappingBranches.Any())
            return Menu.Items;

        // Sort on left to right shown order
        Sorter.Sort(
            overlappingBranches,
            (b1, b2) =>
                b1.X < b2.X ? -1
                : b1.X > b2.X ? 1
                : 0
        );

        // Find possible branch on left side to move to before (skip if ancestor)
        Branch? leftBranch = null;
        for (int i = 0; i < overlappingBranches.Count; i++)
        {
            var b = overlappingBranches[i];
            if (b.B.PrimaryName == branchPrimaryName)
                break;
            leftBranch = b.B;
        }
        leftBranch = leftBranch != null ? repo.Repo.BranchByName[leftBranch.PrimaryName] : null;
        var leftPrimaryName = leftBranch != null && !IsAncestor(leftBranch, branch) ? leftBranch.PrimaryName : "";

        // Find possible branch on right side to move to after (skip if ancestor)
        Branch? rightBranch = null;
        for (int i = overlappingBranches.Count - 1; i >= 0; i--)
        {
            var b = overlappingBranches[i];
            if (b.B.PrimaryName == branchPrimaryName)
                break;
            rightBranch = b.B;
        }
        rightBranch = rightBranch != null ? repo.Repo.BranchByName[rightBranch.PrimaryName] : null;
        var rightPrimaryName = rightBranch != null && !IsAncestor(branch, rightBranch) ? rightBranch.PrimaryName : "";

        var items = Menu.Items;
        // Add menu items if movable branches found
        if (leftPrimaryName != "")
        {
            items.Item(
                $"<= (Move Branch left of {leftBranch!.NiceNameUnique})",
                "",
                () => cmds.MoveBranch(branch.PrimaryName, leftPrimaryName, -1)
            );
        }
        if (rightPrimaryName != "")
        {
            items.Item(
                $"=> (Move right of {rightBranch!.NiceNameUnique})",
                "",
                () => cmds.MoveBranch(branch.PrimaryName, rightPrimaryName, +1)
            );
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
        bool isRowBranchHidable = branches.Any(b => b.IsInView && rowBranch.AncestorNames.Contains(b.Name));

        // Return hidden branches that can be shown, followed by shown branches that can be hidden
        return Menu
            .Items.Separator(hiddenBranches.Any(), "Show")
            .Items(ToBranchesItems(hiddenBranches, b => cmds.ShowBranch(b.Name, false), null, true))
            .Separator(shownBranches.Any() || isRowBranchHidable, "Hide")
            .Items(ToBranchesItems(shownBranches, b => cmds.HideBranch(b.Name, false)))
            .Items(isRowBranchHidable, ToBranchesItems(new[] { rowBranch }, b => cmds.HideBranch(b.Name, false)));
    }

    public IEnumerable<MenuItem> GetShowBranchItems()
    {
        var currentAuthor = repo.CurrentAuthor;
        var allBranches = repo.Repo.AllBranches;

        var liveBranches = allBranches.Where(b => b.IsGitBranch && b.IsPrimary).OrderBy(b => b.NiceNameUnique);

        var myLiveBranches = allBranches
            .Where(b => b.IsGitBranch && b.IsPrimary && repo.Repo.CommitById[b.TipId].Author == currentAuthor)
            .OrderBy(b => b.NiceNameUnique);

        var liveAndDeletedBranches = allBranches.Where(b => b.IsPrimary).OrderBy(b => b.NiceNameUnique).ToList();

        var recentBranches = liveAndDeletedBranches
            .Where(b => b.IsPrimary)
            .OrderBy(b => repo.Repo.CommitById[b.TipId].GitIndex)
            .Take(RecentCount);

        var ambiguousBranches = allBranches.Where(b => b.AmbiguousTipId != "").OrderBy(b => b.NiceNameUnique);

        var items = Menu
            .Items.Items(GetCommitInOutItems())
            .SubMenu(
                "    Recent",
                "",
                ToBranchesItems(recentBranches, ShowBranch)
                    .Prepend(
                        Menu.Item("Show 5 more Recent", "", () => cmds.ShowBranch("", false, ShowBranches.AllRecent, 5))
                    )
            )
            .SubMenu(
                "    Active",
                "",
                ToHierarchicalBranchesItems(liveBranches, ShowBranch)
                    .Prepend(Menu.Item("Show All Active", "", () => cmds.ShowBranch("", false, ShowBranches.AllActive)))
            )
            .SubMenu("    My Active", "", ToHierarchicalBranchesItems(myLiveBranches, ShowBranch))
            .SubMenu(
                "    Active and Deleted",
                "",
                ToHierarchicalBranchesItems(liveAndDeletedBranches, ShowBranch)
                    .Prepend(
                        Menu.Item(
                            "Show All Active and Deleted",
                            "",
                            () => cmds.ShowBranch("", false, ShowBranches.AllActiveAndDeleted)
                        )
                    )
            );

        return ambiguousBranches.Any()
            ? items.SubMenu("    Ambiguous", "", ToBranchesItems(ambiguousBranches, b => cmds.ShowBranch(b.Name, true)))
            : items;
    }

    // Everything about branches, for the commit menu: the branches currently shown in the graph,
    // each item opening that branch's own menu, so a branch operation is reachable without first
    // hoovering the branch with the ← / → keys, followed by the items that show and hide branches
    // and the ones that pull and push all of them.
    public IEnumerable<MenuItem> GetShownBranchesItems()
    {
        var isStatusOK = repo.Repo.Status.IsOk;

        return GetShownBranchesSubMenus()
            .Concat(
                Menu.Items.Separator()
                    .SubMenu("Show/Open Branch", "Shift →", GetShowBranchItems())
                    .Item("Hide All Branches", "", () => cmds.HideBranch("", true))
                    .Item("Pull/Update All Branches", "Shift-U", () => cmds.PullAllBranches())
                    .Item("Push All Branches", "Shift-P", () => cmds.PushAllBranches(), () => isStatusOK)
            );
    }

    // The branch the user is on first, then the branch it was branched from, and so on up to the
    // main branch, since that chain is what an operation is usually about; the remaining shown
    // branches follow in name order. The items are deliberately left deferred (a lazy Select):
    // Menu.Show calls Children.Any() on every submenu of the level it opens, so only the first
    // branch's menu is built while the commit menu is shown, the rest when the user opens the
    // Branches submenu.
    IEnumerable<MenuItem> GetShownBranchesSubMenus() =>
        GetShownBranchesInMenuOrder()
            .Select(b =>
                Menu.SubMenu(
                    // Every branch here is shown, so the 'o' shown icon would carry no information
                    ToBranchMenuName(b, isNoShowIcon: true),
                    "",
                    GetBranchMenuItems(b.PrimaryName, true)
                )
            );

    // The branches the graph draws, as the primary branches the menu is built for, ordered current
    // branch, its ancestors nearest first, then the rest by name.
    IEnumerable<Branch> GetShownBranchesInMenuOrder()
    {
        var rank = GetCurrentBranchChainRanks();
        return repo
            .Graph.GetPageBranches(0, repo.Repo.ViewCommits.Count - 1)
            .Select(gb => gb.B)
            .DistinctBy(b => b.PrimaryName)
            // DistinctBy keeps whichever of a local/remote pair comes first, so resolve to the
            // primary branch, which is the one the menu is built for.
            .Select(b => repo.Repo.BranchByName[b.PrimaryName])
            .OrderBy(b => rank.TryGetValue(b.PrimaryName, out var r) ? r : int.MaxValue)
            .ThenBy(b => b.NiceNameUnique);
    }

    // The current branch and its ancestors, by primary name and by how far up the chain they are.
    // AncestorNames is already ordered parent, grandparent and so on. A local branch has its remote
    // as parent, so a local/remote pair yields the same primary name twice; the first, i.e. the
    // nearest, is the rank that counts.
    IReadOnlyDictionary<string, int> GetCurrentBranchChainRanks()
    {
        var ranks = new Dictionary<string, int>();
        var current = repo.Repo.AllBranches.FirstOrDefault(b => b.IsCurrent);
        if (current == null)
            return ranks; // No current branch (e.g. an empty repo)

        foreach (var name in current.AncestorNames.Prepend(current.Name))
        {
            if (!repo.Repo.BranchByName.TryGetValue(name, out var b))
                continue;
            if (!ranks.ContainsKey(b.PrimaryName))
                ranks[b.PrimaryName] = ranks.Count;
        }

        return ranks;
    }

    void ShowBranch(Branch b) => cmds.ShowBranch(b.Name, false);

    IEnumerable<MenuItem> ToHierarchicalBranchesItems(
        IEnumerable<Branch> branches,
        Action<Branch> action,
        Func<Branch, bool>? canExecute = null,
        bool isNoShowIcon = false
    )
    {
        var filteredBranches = branches.Where(b => b.IsPrimary).DistinctBy(b => b.PrimaryName);
        return ToHierarchicalBranchesItemsImpl(filteredBranches, action, canExecute, isNoShowIcon, 0);
    }

    IEnumerable<MenuItem> ToHierarchicalBranchesItemsImpl(
        IEnumerable<Branch> branches,
        Action<Branch> action,
        Func<Branch, bool>? canExecute,
        bool isNoShowIcon,
        int level
    )
    {
        if (branches.Count() <= MaxItemCount)
        { // Too few branches to bother with submenus
            return ToBranchesItems(branches, action, canExecute, false, isNoShowIcon);
        }

        // Group by first part of the b.commonName (if '/' exists in name)
        var groups = branches
            .GroupBy(b =>
            {
                var parts = b.NiceNameUnique.Split('/', '(');
                if (parts.Length <= level)
                    return parts.Last();
                return parts[level];
            })
            .OrderBy(g => g.Count() > 1 ? 0 : 1) // Sort groups first;
            .ThenBy(g => g.Key);

        // If only one item in group, then just show branch, otherwise show submenu
        // Group name is either group/* or group(*) depending on if all branches in group have same nice name
        string ToGroupName(IGrouping<string, Branch> bs) =>
            bs.All(b => b.NiceName == bs.First().NiceName) ? $"    {bs.Key}(*)" : $"    {bs.Key}/*";
        return groups.Select(g =>
            g.Count() == 1
                ? ToBranchesItems(g, action, canExecute, false, isNoShowIcon).First()
                : Menu.SubMenu(
                    ToGroupName(g),
                    "",
                    ToHierarchicalBranchesItemsImpl(g, action, canExecute, isNoShowIcon, level + 1)
                )
        );
    }

    IEnumerable<MenuItem> ToBranchesItems(
        IEnumerable<Branch> branches,
        Action<Branch> action,
        Func<Branch, bool>? canExecute = null,
        bool canBeOutside = false,
        bool isNoShowIcon = false
    )
    {
        canExecute ??= (b => true);
        return branches.Select(b =>
            Menu.Item(
                ToBranchMenuName(b, canBeOutside, isNoShowIcon),
                ToBranchOwnerInitials(b),
                () => action(b),
                () => canExecute(b)
            )
        );
    }

    string ToBranchOwnerInitials(Branch b)
    {
        var tip = repo.Repo.CommitById[b.TipId];
        var initials = string.Join(
            ' ',
            tip.Author.Split(' ').Select(p => p.Trim()).Where(p => p.Length > 0).Take(2).Select(p => p[0])
        );

        return $"'{initials}'";
    }

    string ToBranchMenuName(Branch branch, bool canBeOutside = false, bool isNoShowIcon = false)
    {
        var cic = repo.RowCommit;
        bool isBranchIn = false;
        bool isBranchOut = false;
        if (canBeOutside && !branch.IsInView)
        { // The branch is currently not shown
            if (cic.ParentIds.Count > 1 && repo.Repo.CommitById[cic.ParentIds[1]].BranchName == branch.Name)
            { // Is a branch merge in '╮' branch
                isBranchIn = true;
            }
            else if (cic.AllChildIds.ContainsBy(id => repo.Repo.CommitById[id].BranchName == branch.Name))
            { // Is branch out '╯' branch
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
        if (b2 == null)
            return false;
        return b2.AncestorNames.Contains(b1.Name);
    }
}
