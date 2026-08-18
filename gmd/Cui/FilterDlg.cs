using gmd.Cui.Common;
using gmd.Server;
using Terminal.Gui;

namespace gmd.Cui;

interface IFilterDlg
{
    R<Server.Commit> Show(Server.Repo repo, Action<Server.Repo> onRepoChanged, ContentView commitsView);
}

class FilterDlg : IFilterDlg
{
    const int MaxResults = 5000;

    // The dialog's own height, i.e. how far down the log view has to move to stay clear of it
    const int DialogHeight = 3;
    readonly IServer server;
    readonly IBranchColorService branchColorService;

    UIDialog dlg = null!;
    UITextField filterField = null!;
    UILabel statusLabel = null!;

    readonly Dictionary<MouseFlags, OnMouseCallback> mouses = [];
    Action<Repo> onRepoChanged = null!;
    Server.Repo orgRepo = null!;
    Server.Repo currentRepo = null!;
    string currentFilter = null!;
    ContentView resultsView = null!;
    R<Server.Commit> selectedCommit = R.Error("No commit selected");
    Text repoInfo = Text.Empty;
    int closeX = 0;

    internal FilterDlg(IServer server, IBranchColorService branchColorService)
    {
        this.server = server;
        this.branchColorService = branchColorService;
    }

    public R<Server.Commit> Show(Server.Repo repo, Action<Server.Repo> onRepoChanged, ContentView commitsView)
    {
        this.orgRepo = repo;
        this.currentRepo = repo;
        this.currentFilter = null!;
        this.onRepoChanged = onRepoChanged;
        this.resultsView = commitsView;

        dlg = new UIDialog(
            "Filter Commits",
            Dim.Fill() + 1,
            DialogHeight,
            OnDialogKey,
            // X = -1 pushes the dialog's left border off screen, so its content lines up with the
            // log view below it. Y = -1 pins it to the top: Terminal.Gui clamps a Toplevel's Y to
            // the screen, so it lands on row 0 rather than the bottom it would otherwise be
            // centered at — but that also means the top border cannot be hidden the way the left
            // one is, which is what Show() has to work around.
            options =>
            {
                options.X = -1;
                options.Y = -1;
            }
        );
        dlg.RegisterMouseHandler(OnMouseEvent);

        dlg.AddLabel(0, 0, Text.BrightMagenta(" Gmd"));
        var searchLabelX = Application.Driver.Cols - 41;
        dlg.AddLabel(searchLabelX, 0, Text.BrightMagenta("Search:"));
        filterField = dlg.AddInputField(searchLabelX + 8, 0, 29);

        closeX = searchLabelX + 8 + 29 + 2;
        var closeButton = dlg.AddLabel(closeX, 0, Text.White("X"));

        filterField.KeyUp += (k) => OnFilterFieldKeyUp(k); // Update results and select commit on keys

        statusLabel = dlg.AddLabel(5, 0);

        // Initializes results with current repo commits
        UI.Post(() => UpdateFilteredResults().RunInBackground());

        // The dialog is drawn over the top of the log view and needs one row more than it has to
        // cover: its border, its one content row and its border again, against the two rows of
        // application bar. So its bottom border lands on the log view's first row. Move the log
        // view down for as long as the dialog is up, or the topmost result is never visible — and
        // a filter matching a single commit looks like it matched none.
        var orgY = commitsView.Y;
        commitsView.Y = DialogHeight;
        try
        {
            dlg.Show(filterField);
        }
        finally
        {
            commitsView.Y = orgY;
        }

        return selectedCommit;
    }

    // User pressed key in filter field, update results
    void OnFilterFieldKeyUp(View.KeyEventEventArgs e)
    {
        UpdateFilteredResults().RunInBackground();
        e.Handled = true;
    }

    bool OnDialogKey(Key key)
    {
        if (key == Key.Enter)
        { // User selected commit from list
            var commit = currentRepo.ViewCommits[resultsView.CurrentIndex];
            if (commit.BranchName != "<none>")
                this.selectedCommit = commit;
            dlg.Close();
            return true;
        }

        // Allow user move up/down in results with keys
        var rsp = StepUpDownInResultList(key);
        ShowCommitInfo();
        return rsp;
    }

    bool StepUpDownInResultList(Key key)
    {
        // Allow user move up/down in results with keys
        switch (key)
        {
            case Key.CursorUp:
                resultsView.Move(-1);
                return true;
            case Key.CursorDown:
                resultsView.Move(1);
                return true;
            case Key.PageUp:
                resultsView.Move(-resultsView.ContentHeight);
                return true;
            case Key.PageDown:
                resultsView.Move(resultsView.ContentHeight);
                return true;
            case Key.Home:
                resultsView.Move(-resultsView.TotalCount);
                return true;
            case Key.End:
                resultsView.Move(resultsView.TotalCount);
                return true;
        }

        return false;
    }

    // Support scrolling with mouse wheel (see ContentView.cs for details)
    bool OnMouseEvent(MouseEvent ev)
    {
        // Log.Info($"OnMouseEvent:  {ev}, {closeX}");
        if (ev.Flags.HasFlag(MouseFlags.Button1Clicked) && ev.X == closeX + 1 && ev.Y == 1)
        {
            dlg.Close();
            return true;
        }

        if (ev.Flags.HasFlag(MouseFlags.WheeledDown))
        {
            resultsView.Scroll(1);
            return true;
        }
        else if (ev.Flags.HasFlag(MouseFlags.WheeledUp))
        {
            resultsView.Scroll(-1);
            return true;
        }

        if (mouses.TryGetValue(ev.Flags, out var callback))
        {
            callback(ev.X, ev.Y);
            return true;
        }

        return false;
    }

    async Task UpdateFilteredResults()
    {
        var filter = filterField.Text.Trim();
        if (filter == currentFilter)
            return;
        currentFilter = filter;

        if (
            filter != ""
            && Try(out var filteredRepo, out var e, await server.GetFilteredRepoAsync(orgRepo, filter, MaxResults))
        )
        { // Got new filtered repo, update results
            currentRepo = filteredRepo;
            resultsView.MoveToTop();
        }
        else
        { // Restore original repo
            currentRepo = orgRepo;
        }

        repoInfo = GetRepoInfo();
        ShowCommitInfo();
        onRepoChanged(currentRepo);
    }

    void ShowCommitInfo()
    {
        var index = resultsView.CurrentIndex;
        if (currentRepo.ViewCommits.Count == 0 || index >= currentRepo.ViewCommits.Count)
        {
            statusLabel.Text = repoInfo;
            return;
        }
        ;

        var commit = currentRepo.ViewCommits[index];
        var branch = currentRepo.BranchByName[commit.BranchName];
        var color = branchColorService.GetColor(currentRepo, branch);
        statusLabel.Text = Text.Add(repoInfo).Cyan($" {commit.Sid}").Color(color, $" ({branch.NiceNameUnique})");
    }

    Text GetRepoInfo()
    {
        var commitCount = currentRepo.ViewCommits.Count(c => c.BranchName != "<none>");
        var branchCount = currentRepo
            .ViewCommits.Select(c => c.BranchPrimaryName)
            .Where(b => b != "<none>")
            .Distinct()
            .Count();
        return Text.Dark($"{commitCount} commits, {branchCount} branches,");
    }
}
