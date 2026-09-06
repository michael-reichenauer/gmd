using gmd.Cui.Common;
using gmd.Server;
using Terminal.Gui;

namespace gmd.Cui.RepoView;

// What the input handling needs from the repo view it drives, beyond what it does itself.
// Implemented by RepoView, which replaces ViewRepo and Menus every time a repo is shown.
interface IRepoViewInputHost
{
    IViewRepo ViewRepo { get; }
    IRepoViewMenus Menus { get; }

    void ToggleDetails();
    void ToggleDetailsFocus();
    void RefreshAndFetch(string addName = "", string commitId = "");
}

// All keys and mouse buttons of the repo view: the table of what is bound to what, and the
// handlers it binds to. Most handlers act on the hoovered branch when there is one and on the
// current commit when there is not, so the hoover is what they mostly consist of, see Hoover.
//
// The handlers themselves only decide and dispatch; the work is done by the command classes
// (RepoCommands / BranchCommands / CommitCommands), the menus, or the view via the host.
class RepoViewInput
{
    readonly IRepoViewInputHost host;
    readonly ContentView commitsView;
    readonly ICommitDetailsView commitDetailsView;
    readonly IApplicationBar applicationBarView;
    readonly IUnicodeSetsDlg charDlg;
    readonly IClipboardService clipboard;
    readonly Hoover hoover;

    bool isRegistered = false;

    internal RepoViewInput(
        IRepoViewInputHost host,
        ContentView commitsView,
        ICommitDetailsView commitDetailsView,
        IApplicationBar applicationBarView,
        IUnicodeSetsDlg charDlg,
        IClipboardService clipboard,
        Hoover hoover
    )
    {
        this.host = host;
        this.commitsView = commitsView;
        this.commitDetailsView = commitDetailsView;
        this.applicationBarView = applicationBarView;
        this.charDlg = charDlg;
        this.clipboard = clipboard;
        this.hoover = hoover;
    }

    // The repo and the menus of the currently shown repo. Both are replaced every time a repo is
    // shown, so they are read from the host rather than stored.
    IViewRepo Repo => host.ViewRepo;
    Server.Repo ServerRepo => host.ViewRepo.Repo;
    IRepoViewMenus Menus => host.Menus;
    IRepoCommands Cmd => Repo.Cmds;
    ICommitCommands CommitCmds => Repo.CommitCmds;
    IBranchCommands BranchCmds => Repo.BranchCmds;

    public void Register()
    {
        if (isRegistered)
        {
            return;
        }
        isRegistered = true;

        // Keys on repo view contents
        commitsView.RegisterKeyHandler(Key.Esc, () => UI.Shutdown());

        // Both cases quit, since the help guide documents this key as 'Q'. Note that keys are
        // looked up by exact value, so a case is only handled if it is registered: 'p' and 'P'
        // below are deliberately different commands, and that is the reason to be explicit here
        // rather than to fold case anywhere central.
        commitsView.RegisterKeyHandler(Key.q, () => UI.Shutdown());
        commitsView.RegisterKeyHandler(Key.Q, () => UI.Shutdown());
        commitsView.RegisterKeyHandler(Key.C | Key.CtrlMask, () => Copy());
        commitsView.RegisterKeyHandler(Key.m, () => OnMenu());
        commitsView.RegisterKeyHandler(Key.o, () => Menus.ShowOpenRepoMenu());
        commitsView.RegisterKeyHandler(Key.CursorLeft, () => OnCursorLeft());
        commitsView.RegisterKeyHandler(Key.CursorRight, () => OnCursorRight());
        commitsView.RegisterKeyHandler(Key.CursorUp, () => OnCursorUp());
        commitsView.RegisterKeyHandler(Key.CursorDown, () => OnCursorDown());
        commitsView.RegisterKeyHandler(Key.CursorRight | Key.ShiftMask, OnKeyShiftCursorRight);

        commitsView.RegisterKeyHandler(Key.r, () => host.RefreshAndFetch());
        commitsView.RegisterKeyHandler(Key.F5, () => host.RefreshAndFetch());
        commitsView.RegisterKeyHandler(Key.c, () => CommitCmds.Commit(false));
        commitsView.RegisterKeyHandler(Key.a, () => CommitCmds.Commit(true));
        commitsView.RegisterKeyHandler(Key.t, () => CommitCmds.AddTag());
        commitsView.RegisterKeyHandler(Key.b, () => CreateBranch());
        commitsView.RegisterKeyHandler(Key.d, OnKeyD);
        commitsView.RegisterKeyHandler(Key.D | Key.CtrlMask, () => CommitCmds.ShowCurrentRowDiff());
        commitsView.RegisterKeyHandler(Key.p, () => BranchCmds.PushCurrentBranch());
        commitsView.RegisterKeyHandler(Key.P, () => BranchCmds.PushAllBranches());
        commitsView.RegisterKeyHandler(Key.u, () => BranchCmds.PullCurrentBranch());
        commitsView.RegisterKeyHandler(Key.U, () => BranchCmds.PullAllBranches());
        commitsView.RegisterKeyHandler(Key.D1, () => Cmd.ShowHelp());
        commitsView.RegisterKeyHandler(Key.F1, () => Cmd.ShowHelp());
        commitsView.RegisterKeyHandler((Key)63, () => Cmd.ShowHelp()); // '?' key
        commitsView.RegisterKeyHandler(Key.f, () => OnKeyF());
        commitsView.RegisterKeyHandler(Key.D0, () => charDlg.Show());
        commitsView.RegisterKeyHandler(Key.D5, () => BranchCmds.SetBranchManuallyAsync());

        commitsView.RegisterKeyHandler(Key.y, () => BranchCmds.ShowBranch(ServerRepo.CurrentBranch().Name, false));
        commitsView.RegisterKeyHandler(Key.s, OnKeyS);
        commitsView.RegisterKeyHandler(Key.e, OnKeyE);
        commitsView.RegisterKeyHandler(Key.E, OnKeyShiftE);
        commitsView.RegisterKeyHandler(Key.h, () => BranchCmds.HideBranch(GetBranchName()));
        commitsView.RegisterKeyHandler(Key.w, () => BranchCmds.ShowWorktrees());

        commitsView.RegisterKeyHandler(Key.Enter, OnKeyEnter);
        commitsView.RegisterKeyHandler(Key.Tab, host.ToggleDetailsFocus);
        commitsView.RegisterKeyHandler(Key.g, () => BranchCmds.ChangeBranchColor(GetBranchName()));

        commitsView.RegisterMouseHandler(MouseFlags.Button1Clicked, (x, y) => OnClicked(x + 1, y));
        commitsView.RegisterMouseHandler(MouseFlags.Button2Clicked, (x, y) => OnClickedMiddle(x + 1, y));

        commitsView.RegisterMouseHandler(MouseFlags.Button1DoubleClicked, (x, y) => OnDoubleClicked(x + 1, y));
        commitsView.RegisterMouseHandler(MouseFlags.Button3Pressed, (x, y) => OnRightClicked(x + 1, y));
        commitsView.RegisterMouseHandler(MouseFlags.ReportMousePosition, (x, y) => OnMouseMoved(x + 1, y));

        // Keys on commit details view.
        commitDetailsView.View.RegisterKeyHandler(Key.Tab, () => host.ToggleDetailsFocus());
        commitDetailsView.View.RegisterKeyHandler(Key.d, () => CommitCmds.ShowCurrentRowDiff());

        applicationBarView.ItemClicked += OnApplicationClick;
    }

    void CreateBranch()
    {
        if (hoover.IsBranch)
        {
            BranchCmds.CreateBranchFromBranch(hoover.BranchPrimaryName);
            return;
        }

        BranchCmds.CreateBranchFromCommit();
    }

    void OnKeyShiftCursorRight()
    {
        var x = !hoover.IsBranch ? Repo.Graph.Width + 2 : hoover.ColumnIndex;
        var y = hoover.RowIndex == -1 ? Repo.CurrentIndex : hoover.RowIndex;
        Menus.ShowOpenBranchMenu(x + 2, y + 1);
    }

    void OnKeyD()
    {
        if (hoover.IsBranch)
        {
            Menus.ShowDiffBranchToMenu(hoover.ColumnIndex + 2, hoover.RowIndex + 1, hoover.BranchName);
            return;
        }

        CommitCmds.ShowCurrentRowDiff();
    }

    string GetBranchName() => hoover.IsBranch ? hoover.BranchPrimaryName : Repo.RowBranch.Name;

    void OnKeyF()
    {
        ClearHoover();
        Cmd.SearchFilterRepo();
    }

    void OnApplicationClick(int x, int y, ApplicationBarItem item)
    {
        switch (item)
        {
            case ApplicationBarItem.Update:
                Cmd.UpdateRelease();
                break;
            case ApplicationBarItem.Gmd:
                Menus.ShowRepoMenu(x - 5, y);
                break;
            case ApplicationBarItem.Repo:
                Menus.ShowOpenRepoMenu(x - 5, y);
                break;
            case ApplicationBarItem.CurrentBranch:
                BranchCmds.ShowBranch(ServerRepo.CurrentBranch().Name, false);
                break;
            case ApplicationBarItem.Status:
                CommitCmds.CommitFromMenu(false);
                break;
            case ApplicationBarItem.Behind:
                BranchCmds.PullAllBranches();
                break;
            case ApplicationBarItem.Ahead:
                BranchCmds.PushAllBranches();
                break;
            case ApplicationBarItem.BranchName:
                Menus.ShowOpenBranchMenu(x - 5, y);
                break;
            case ApplicationBarItem.Stash:
                Menus.ShowStashMenu(x - 5, y);
                break;
            case ApplicationBarItem.Worktrees:
                BranchCmds.ShowWorktrees();
                break;
            case ApplicationBarItem.Search:
                Cmd.SearchFilterRepo();
                break;
            case ApplicationBarItem.Help:
                Cmd.ShowHelp();
                break;
            case ApplicationBarItem.Close:
                UI.Shutdown();
                break;
        }
    }

    void OnKeyE()
    {
        if (hoover.IsBranch)
        {
            var branch = ServerRepo.BranchByName[hoover.BranchPrimaryName];
            if (branch.LocalName != "")
                branch = ServerRepo.BranchByName[branch.LocalName];
            if (!branch.IsCurrent && ServerRepo.Status.IsOk)
            { // Some other branch merging to current
                BranchCmds.MergeBranch(hoover.BranchPrimaryName);
                return;
            }

            if (branch.IsCurrent && ServerRepo.Status.IsOk)
            { // Current branch showing menu of branches to merge from
                var hb = Repo.Graph.BranchByName(branch.Name);
                Menus.ShowMergeFromMenu(hb.X * 2 + 3, Repo.CurrentIndex + 1);
                return;
            }
        }
    }

    // The mirror of OnKeyE: 'e' merges into the current branch, 'E' merges the current branch out
    // into another one, which git can only do by checking that one out on the way.
    void OnKeyShiftE()
    {
        if (hoover.IsBranch)
        {
            var branch = ServerRepo.BranchByName[hoover.BranchPrimaryName];
            if (branch.LocalName != "")
                branch = ServerRepo.BranchByName[branch.LocalName];
            // A branch git no longer has would be recreated by the checkout, so it is not offered,
            // exactly as in the branch menu
            if (!branch.IsCurrent && branch.IsGitBranch && ServerRepo.Status.IsOk)
            { // Current branch merging to some other branch
                BranchCmds.MergeToBranch(branch.Name);
                return;
            }

            if (branch.IsCurrent && ServerRepo.Status.IsOk)
            { // Current branch showing menu of branches to merge to
                var hb = Repo.Graph.BranchByName(branch.Name);
                Menus.ShowMergeToMenu(hb.X * 2 + 3, Repo.CurrentIndex + 1);
                return;
            }
        }
    }

    void OnKeyS()
    {
        if (hoover.IsBranch)
        {
            var branchName = hoover.BranchPrimaryName;
            var currentName = ServerRepo.CurrentBranch().PrimaryName;
            var branch = ServerRepo.BranchByName[branchName];
            if (branch.LocalName != "")
                branchName = branch.LocalName;

            if (branch.PrimaryName != currentName)
            {
                BranchCmds.SwitchTo(branchName);
            }

            return;
        }
    }

    void OnKeyEnter()
    {
        if (hoover.IsBranch)
        {
            var branch = Repo
                .Graph.GetRowBranches(Repo.CurrentIndex)
                .FirstOrDefault(b => b.B.PrimaryName == hoover.BranchPrimaryName);
            if (branch != null)
            {
                TryShowHideCommitBranch(branch.X * 2 + 3, commitsView.CurrentPoint.Y + 1);
                return;
            }

            return;
        }

        host.ToggleDetails();
    }

    void OnMenu()
    {
        if (hoover.IsBranch)
        {
            var branch = Repo
                .Graph.GetRowBranches(Repo.CurrentIndex)
                .FirstOrDefault(b => b.B.PrimaryName == hoover.BranchPrimaryName);
            if (branch == null)
            {
                ClearHoover();
                return;
            }

            Menus.ShowBranchMenu(branch.X * 2 + 3, commitsView.CurrentIndex + 1, hoover.BranchPrimaryName);
            return;
        }

        Menus.ShowCommitMenu(Repo.Graph.Width + 5, commitsView.CurrentIndex + 1, commitsView.CurrentIndex);
    }

    void OnCursorLeft()
    {
        var branches = Repo.Graph.GetRowBranches(Repo.CurrentIndex);

        // Nothing happens at the left side of a row. Continuing to the branches further down the
        // page that are further to the left was tried once and disabled again, since a cursor key
        // that also scrolls the view was confusing.
        var branch = hoover.NextLeft(branches);
        if (branch != null)
        {
            SetHooverBranch(branch, Repo.CurrentIndex);
        }
    }

    void OnCursorRight()
    {
        if (!hoover.IsBranch)
            return; // Commit is hoovered or selected

        var branches = Repo.Graph.GetRowBranches(Repo.CurrentIndex);

        // The right side of a row is where the commit is selected instead. Continuing to the
        // branches further up the page that are further to the right was tried once and disabled
        // again, for the same reason as moving left.
        var branch = hoover.NextRight(branches);
        if (branch == null)
        { // Reached right side, or the hoover branch is gone, select the commit instead
            ClearHoover();
            return;
        }

        SetHooverBranch(branch, Repo.CurrentIndex);
    }

    void OnCursorUp() => MoveCursor(-1);

    void OnCursorDown() => MoveCursor(1);

    // Moving the current row up or down keeps the hoover on its branch, so that holding a cursor
    // key follows a branch down the graph rather than dropping off it at the first row it is not
    // drawn in the same column.
    void MoveCursor(int delta)
    {
        // Store current hover branch (if any)
        var hooverName = hoover.BranchPrimaryName;
        var hooverColumnIndex = hooverName != "" ? hoover.ColumnOf(Repo.Graph.GetRowBranches(Repo.CurrentIndex)) : -1;

        commitsView.ClearSelection();
        ClearHoover();
        commitsView.Move(delta);

        if (hooverName != "")
        { // Try restore hover branch after moving
            // Try locate the same branch on the previous row or the next branch on the previous row
            var branches = Repo.Graph.GetRowBranches(Repo.CurrentIndex);
            SetHooverBranch(Hoover.Locate(branches, hooverName, hooverColumnIndex), Repo.CurrentIndex);
        }
    }

    void Copy()
    {
        var selection = commitsView.Selection;
        if (selection.IsEmpty)
            return;

        var (i1, i2) = (selection.I1, selection.I2);
        if (i1 == i2)
        { // Select within the same commit
            CopyToClipboard(commitsView.CopySelectedText());
            return;
        }

        CopyToClipboard(SelectedCommitsText(ServerRepo.ViewCommits, i1, i2));
        commitsView.ClearSelection();
        commitsView.SetNeedsDisplay();
    }

    // A copy that fails must say so: a clipboard tool that is missing or that cannot reach the
    // session is the normal way for this to fail, and silence just looks like a key that does
    // nothing.
    void CopyToClipboard(string text)
    {
        if (!Try(out var e, clipboard.Set(text)))
            UI.ErrorMessage(e.AllErrorMessages());
    }

    // The text of a selection that spans several commits: the sid and subject of each selected
    // commit, but only of the commits on the same branch as the first selected commit, since the
    // rows in between belong to whichever branches the graph happens to draw there.
    internal static string SelectedCommitsText(IReadOnlyList<Commit> commits, int i1, int i2)
    {
        var branch = commits[i1].BranchPrimaryName;
        return commits
            .Skip(i1)
            .Take(i2 - i1 + 1)
            .Where(c => c.BranchPrimaryName == branch)
            .Select(c => $"{c.Sid} {c.Subject}")
            .Join("\n");
    }

    void OnDoubleClicked(int x, int y)
    {
        var index = y + commitsView.FirstIndex;
        commitsView.SetCurrentIndex(index);

        if (x > Repo.Graph.Width)
        {
            ClearHoover();
            host.ToggleDetails();
        }

        if (Repo.Graph.TryGetBranchByPos(x, index, out var branch))
        { // Clicked on a branch, try to show/hide branch if point is a e.g. a merge, branch-out commit
            var currentName = ServerRepo.CurrentBranch().PrimaryName;

            var branchName = branch.B.Name;
            if (branch.B.LocalName != "")
                branchName = branch.B.LocalName;

            if (branch.B.PrimaryName != currentName)
            {
                BranchCmds.SwitchTo(branchName);
            }
            return;
        }
    }

    void OnClicked(int x, int y)
    {
        var index = y + commitsView.FirstIndex;
        commitsView.SetCurrentIndex(index);

        if (
            Repo.Graph.TryGetBranchByPos(x, index, out var branch)
            && Repo.RowCommit.BranchPrimaryName == branch.B.PrimaryName
        )
        { // Clicked on a branch, try to show/hide branch if point is a e.g. a merge, branch-out commit
            TryShowHideCommitBranch(x, y);
            return;
        }

        if (x > Repo.Graph.Width)
        { // Clicked on a commit
            ClearHoover();
            return;
        }
    }

    void OnClickedMiddle(int x, int y)
    {
        var index = y + commitsView.FirstIndex;
        commitsView.SetCurrentIndex(index);

        if (Repo.Graph.TryGetBranchByPos(x, index, out var branch))
        { // Clicked on a branch, try to show/hide branch if point is a e.g. a merge, branch-out commit
            var hb = branch.B;
            if (hb.LocalName != "")
                hb = ServerRepo.BranchByName[hb.LocalName];
            if (!hb.IsCurrent && ServerRepo.Status.IsOk)
            { // Some other branch merging to current
                BranchCmds.MergeBranch(hb.Name);
                return;
            }

            return;
        }
    }

    void OnRightClicked(int x, int y)
    {
        int index = y + commitsView.FirstIndex;
        commitsView.SetCurrentIndex(index);

        if (x > Repo.Graph.Width)
        { // Right-clicked on commit, show commit menu
            Menus.ShowCommitMenu(x, y + 1, index);
            ClearHoover();
            return;
        }

        if (Repo.Graph.TryGetBranchByPos(x, index, out var branch))
        { // Right-clicked on branch, show commit menu
            Menus.ShowBranchMenu(x + 2, y + 1, branch.B.Name);
            ClearHoover();
            return;
        }
    }

    bool OnMouseMoved(int x, int y)
    {
        SetHoover(x, y);
        return false;
    }

    void SetHoover(int x, int y)
    {
        if (x > Repo.Graph.Width)
        {
            SetHoverCommit(x, y);
            return;
        }

        var index = y + commitsView.FirstIndex;
        if (Repo.Graph.TryGetBranchByPos(x, index, out var branch))
        { // Moved over a branch
            SetHooverBranch(branch, index);
            return;
        }

        ClearHoover();
    }

    void SetHoverCommit(int x, int y)
    {
        if (x > Repo.Graph.Width)
        {
            var index = y + commitsView.FirstIndex;
            hoover.SetCommit(index, Repo.Graph.Width + 2, Repo.CurrentIndex);
            commitsView.SetNeedsDisplay();
            return;
        }
    }

    void SetHooverBranch(GraphBranch branch, int index)
    {
        if (hoover.SetBranch(branch, index, Repo.CurrentIndex))
        {
            applicationBarView.SetBranch(branch);
            commitsView.SetNeedsDisplay();
        }
    }

    void ClearHoover()
    {
        if (hoover.Clear())
        {
            commitsView.SetNeedsDisplay();
        }
    }

    void TryShowHideCommitBranch(int x, int y)
    {
        var commitBranches = Repo.GetCommitBranches(true);

        if (commitBranches.Count == 0)
            return;

        if (commitBranches.Count == 1)
        { // Only one possible branch, toggle shown
            if (commitBranches[0].IsInView)
                BranchCmds.HideBranch(commitBranches[0].Name);
            else
                BranchCmds.ShowBranch(commitBranches[0].Name, false);
            return;
        }

        Menus.ShowCommitBranchesMenu(x, y);
    }
}
