using gmd.Common;
using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.RepoView;



interface IRepoViewMenus
{
    void ShowRepoMenu(int x, int y);
    void ShowCommitMenu(int x, int y, int index);
    void ShowBranchMenu(int x, int y, string branchName);
    void ShowCommitBranchesMenu(int x, int y);
    void ShowOpenBranchesMenu(int x = Menu.Center, int y = 0);
    void ShowStashMenu(int x = Menu.Center, int y = 0);
    void ShowMergeFromMenu(int x = Menu.Center, int y = 0);
    void ShowOpenRepoMenu(int x = Menu.Center, int y = 0);
    void ShowDiffBranchToMenu(int x, int y, string branchName);

    IEnumerable<MenuItem> GetNewReleaseItems();
    IEnumerable<MenuItem> GetRepoMenuItems();
}


class RepoViewMenus : IRepoViewMenus
{
    const int RecentCount = 15;
    const int MaxItemCount = 20;

    readonly ICommitMenu commitMenu;
    readonly IBranchMenu branchMenu;
    readonly IRepo repo;
    readonly IRepoCommands cmds;
    readonly Config config;
    readonly IConfigDlg configDlg;
    internal RepoViewMenus(IRepo repo, Config config, IConfigDlg configDlg)
    {
        branchMenu = new BranchMenu(this, repo, repo.Cmd);
        commitMenu = new CommitMenu(this, branchMenu, repo, repo.Cmd);
        this.repo = repo;
        this.cmds = repo.Cmd;
        this.config = config;
        this.configDlg = configDlg;
    }

    public void ShowRepoMenu(int x, int y)
    {
        Menu.Show($"Repo Menu", x, y + 2, GetRepoMenuItems());
    }

    public void ShowCommitMenu(int x, int y, int index) => commitMenu.Show(x, y, index);

    public void ShowBranchMenu(int x, int y, string branchName) => branchMenu.Show(x, y, branchName);

    public void ShowCommitBranchesMenu(int x, int y) => branchMenu.ShowCommitBranchesMenu(x, y);


    public void ShowMergeFromMenu(int x = Menu.Center, int y = 0) => branchMenu.ShowMergeFromMenu(x, y);

    public void ShowOpenRepoMenu(int x = Menu.Center, int y = 0)
    {
        Menu.Show("Open Repo", x, y + 2, GetOpenRepoItems());
    }

    public void ShowOpenBranchesMenu(int x = Menu.Center, int y = 0) => branchMenu.ShowOpenBranchesMenu(x, y);

    public void ShowStashMenu(int x = Menu.Center, int y = 0) => commitMenu.ShowStashMenu(x, y);


    public void ShowDiffBranchToMenu(int x, int y, string branchName) =>
        branchMenu.ShowDiffBranchToMenu(x, y, branchName);

    public IEnumerable<MenuItem> GetRepoMenuItems()
    {
        var isStatusOK = repo.Repo.Status.IsOk;

        return Menu.Items
            .Item("Pull/Update All Branches", "Shift-U", () => cmds.PullAllBranches(), () => isStatusOK)
            .Item("Push All Branches", "Shift-P", () => cmds.PushAllBranches(), () => isStatusOK)
            .Item("Search/Filter ...", "F", () => cmds.Filter())
            .Item("Refresh/Reload", "R", () => cmds.RefreshAndFetch())
            .Item("Clean/Restore Working Folder", "", () => cmds.CleanWorkingFolder())
            .SubMenu("Open/Clone/Init Repo", "O", GetOpenRepoItems())
            .Item("Config ...", "", () => configDlg.Show(repo.Repo.Path))
            .Item("Help ...", "?, F1", () => cmds.ShowHelp())
            .Item("About ...", "", () => cmds.ShowAbout())
            .Item("Quit", "Q, Esc", () => UI.Shutdown());
    }


    public IEnumerable<MenuItem> GetNewReleaseItems()
    {
        if (!config.Releases.IsUpdateAvailable()) return Menu.Items;
        return Menu.Items
           .Separator("New Release Available !!!")
           .Item("Update to Latest Version ...", "", () => cmds.UpdateRelease())
           .Separator();
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



    IEnumerable<MenuItem> GetOpenRepoItems() => Menu.Items
        .Items(GetRecentRepoItems())
        .Separator()
        .Item("Browse ...", "", () => cmds.ShowBrowseDialog())
        .Item("Clone ...", "", () => cmds.Clone())
        .Item("Init ...", "", () => cmds.InitRepo());


    IEnumerable<MenuItem> GetRecentRepoItems() =>
        config.RecentFolders
            .Where(Directory.Exists)
            .Take(10)
            .Select(path => Menu.Item(path, "", () => cmds.ShowRepo(path), () => path != repo.Repo.Path));


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

    static bool IsAncestor(Branch b1, Branch? b2)
    {
        if (b2 == null) return false;
        return b2.AncestorNames.Contains(b1.Name);
    }

    public static string Sid(string id) => id == Repo.UncommittedId ? "uncommitted" : id.Sid();
}
