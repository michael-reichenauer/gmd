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
    readonly IServer server;
    readonly IBranchColorService branchColorService;

    UIDialog dlg = null!;
    UITextField filterField = null!;
    UILabel statusLabel = null!;

    readonly Dictionary<MouseFlags, OnMouseCallback> mouses = new Dictionary<MouseFlags, OnMouseCallback>();
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

        dlg = new UIDialog("Filter Commits", Dim.Fill() + 1, 3, OnDialogKey, options => { options.X = -1; options.Y = -1; });
        dlg.RegisterMouseHandler(OnMouseEvent);
        Log.Info($"FilterDlg.Show: {Application.Driver.Cols}");

        var filterX = Application.Driver.Cols - 33;
        dlg.AddLabel(0, 0, Text.BrightMagenta("Gmd"));
        closeX = filterX + 31;
        var closeButton = dlg.AddLabel(closeX, 0, Text.BrightMagenta("X"));

        dlg.AddLabel(Application.Driver.Cols - 41, 0, Text.BrightMagenta("Search:"));
        filterField = dlg.AddInputField(filterX, 0, 29);
        filterField.KeyUp += (k) => OnFilterFieldKeyUp(k);    // Update results and select commit on keys

        statusLabel = dlg.AddLabel(6, 0);

        // Initializes results with current repo commits
        UI.Post(() => UpdateFilteredResults().RunInBackground());

        dlg.Show(filterField);

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
        {   // User selected commit from list
            var commit = currentRepo.Commits[resultsView.CurrentIndex];
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
        if (filter == currentFilter) return;
        currentFilter = filter;

        if (filter != "" && Try(out var filteredRepo, out var e, await server.GetFilteredRepoAsync(orgRepo, filter, MaxResults)))
        {   // Got new filtered repo, update results
            currentRepo = filteredRepo;
            resultsView.MoveToTop();
        }
        else
        {   // Restore original repo
            currentRepo = orgRepo;
        }

        repoInfo = GetRepoInfo();
        ShowCommitInfo();
        onRepoChanged(currentRepo);
    }


    void ShowCommitInfo()
    {
        var index = resultsView.CurrentIndex;
        if (currentRepo.Commits.Count == 0 || index >= currentRepo.Commits.Count)
        {
            statusLabel.Text = repoInfo;
            return;
        };

        var commit = currentRepo.Commits[index];
        var branch = currentRepo.BranchByName[commit.BranchName];
        var color = branchColorService.GetColor(currentRepo, branch);
        statusLabel.Text = Text.Add(repoInfo).Cyan($" {commit.Sid}").Color(color, $" ({branch.NiceNameUnique})");
    }


    Text GetRepoInfo()
    {
        var commitCount = currentRepo.Commits.Count(c => c.BranchName != "<none>");
        var branchCount = currentRepo.Commits.Select(c => c.BranchPrimaryName).Where(b => b != "<none>").Distinct().Count();
        return Text.Dark($"{commitCount} commits, {branchCount} branches,");
    }
}




