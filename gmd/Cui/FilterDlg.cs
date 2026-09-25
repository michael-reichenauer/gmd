using gmd.Cui.Common;
using gmd.Server;
using Terminal.Gui;

namespace gmd.Cui;

interface IFilterDlg
{
    Result<SearchPick> Show(Server.Repo repo, Action<Server.Repo> onRepoChanged, ContentView commitsView);
}

// The commit picked in the search, with what was searched for and every commit it found, in the
// order listed, for stepping through them in the log afterwards (SearchMatches)
record SearchPick(Server.Commit Commit, string Filter, IReadOnlyList<string> MatchIds);

class FilterDlg : IFilterDlg
{
    const int MaxResults = 5000;

    // How long typing has to pause before the files a search names are asked of git, which takes a
    // moment in a large repo, rather than once for every key of a path being typed
    static readonly TimeSpan FileSearchDelay = TimeSpan.FromMilliseconds(300);

    // The dialog's own height, i.e. how far down the log view has to move to stay clear of it
    const int DialogHeight = 3;
    readonly IServer server;
    readonly IBranchColorService branchColorService;

    UIDialog dlg = null!;
    UITextField filterField = null!;
    UILabel statusLabel = null!;

    Action<Repo> onRepoChanged = null!;
    Server.Repo orgRepo = null!;
    Server.Repo currentRepo = null!;
    string currentFilter = null!;
    ContentView resultsView = null!;
    Result<SearchPick> selectedCommit;
    Text repoInfo = Text.Empty;
    int closeX = 0;

    // Which showing of the dialog this is, since it is reused, and whether it is up, so that a search
    // still running when it closes knows its answer is no longer wanted: shown then, it replaced the
    // log the user had gone back to with the results, the dialog gone
    int session;
    bool isOpen;

    internal FilterDlg(IServer server, IBranchColorService branchColorService)
    {
        this.server = server;
        this.branchColorService = branchColorService;
    }

    public Result<SearchPick> Show(Server.Repo repo, Action<Server.Repo> onRepoChanged, ContentView commitsView)
    {
        this.orgRepo = repo;
        this.currentRepo = repo;
        this.currentFilter = null!;
        // The dialog is reused, so what an earlier session selected must not be returned by this one
        this.selectedCommit = new Error("No commit selected");
        this.onRepoChanged = onRepoChanged;
        this.resultsView = commitsView;
        this.session++;
        this.isOpen = true;

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
            isOpen = false;
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
            {
                var matchIds = currentRepo == orgRepo ? [] : currentRepo.ViewCommits.Select(c => c.Id).ToList();
                this.selectedCommit = new SearchPick(commit, currentFilter ?? "", matchIds);
            }
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

    // The close X, the mouse wheel, and a click on a result
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

        // A click on a result picks it, as Enter does. The results are the log view's rows, below the
        // dialog, which has the mouse, so the row is worked out from the dialog's own coordinates.
        var resultRow = ev.Y - DialogHeight;
        var isOnResult =
            resultRow >= 0
            && resultRow < resultsView.ViewHeight
            && resultsView.FirstIndex + resultRow < currentRepo.ViewCommits.Count;
        if (ev.Flags.HasFlag(MouseFlags.Button1Clicked) && isOnResult)
        {
            resultsView.SetIndexAtViewY(resultRow);
            ShowCommitInfo();
            return OnDialogKey(Key.Enter);
        }

        return false;
    }

    async Task UpdateFilteredResults()
    {
        var session = this.session;
        var filter = filterField.Text.Trim();
        if (!isOpen || filter == currentFilter)
            return;
        currentFilter = filter;

        var terms = SearchTerms.Parse(filter);
        if (terms.Files.Count > 0)
        { // Git is asked, once typing pauses, and what it answers is dropped if typing went on
            await Task.Delay(FileSearchDelay);
            if (!IsStillWanted(filter, session))
                return;
            statusLabel.Text = Text.Dark("Searching the changed files ...");
        }

        // Nothing to search for is the whole log, as is 'file:' while the path is still to be typed
        Server.Repo? filteredRepo = null;
        if (terms.Words.Count + terms.Files.Count > 0)
        {
            var result = await server.GetFilteredRepoAsync(orgRepo, filter, MaxResults);
            if (!IsStillWanted(filter, session))
                return;
            if (result is Server.Repo repo)
                filteredRepo = repo;
            else
                Log.Warn($"Failed to search, {result.Error}");
        }

        if (filteredRepo != null)
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

    // Whether a search that had to wait is still the one to show: not if typing went on, nor once
    // the dialog has closed, or closed and been shown again
    bool IsStillWanted(string filter, int session)
    {
        if (!isOpen || session != this.session)
        {
            Log.Info("Search dropped, the search closed before it was done");
            return false;
        }

        return filter == currentFilter;
    }

    void ShowCommitInfo()
    {
        var index = resultsView.CurrentIndex;
        if (currentRepo.ViewCommits.Count == 0 || index >= currentRepo.ViewCommits.Count)
        {
            statusLabel.Text = repoInfo;
            return;
        }

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
