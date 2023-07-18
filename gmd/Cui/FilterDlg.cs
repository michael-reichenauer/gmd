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
    const int MaxResults = int.MaxValue;
    readonly IServer server;
    readonly IBranchColorService branchColorService;

    UIDialog dlg = null!;
    UITextField filterField = null!;
    UILabel statusLabel = null!;

    int mouseEventX = -1;
    int mouseEventY = -1;
    readonly Dictionary<MouseFlags, OnMouseCallback> mouses = new Dictionary<MouseFlags, OnMouseCallback>();
    Action<Repo> onRepoChanged = null!;
    Server.Repo orgRepo = null!;
    Server.Repo currentRepo = null!;
    string currentFilter = null!;
    ContentView resultsView = null!;
    R<Server.Commit> selectedCommit = R.Error("No commit selected");
    Text repoInfo = Text.Empty;


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

        dlg.AddLabel(0, 0, Text.BrightMagenta("Search:"));
        filterField = dlg.AddTextField(9, 0, 30);
        filterField.KeyUp += (k) => OnFilterFieldKeyUp(k);    // Update results and select commit on keys

        statusLabel = dlg.AddLabel(41, 0);

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
        // Log.Info($"OnMouseEvent:  {ev}");

        // On linux (at least dev container console), there is a bug that sends same last mouse event
        // whenever mouse is moved, to still support scroll, we check mouse position.
        bool isSamePos = (ev.X == mouseEventX && ev.Y == mouseEventY);
        mouseEventX = ev.X;
        mouseEventY = ev.Y;

        if (ev.Flags.HasFlag(MouseFlags.WheeledDown) && isSamePos)
        {
            resultsView.Scroll(1);
            return true;
        }
        else if (ev.Flags.HasFlag(MouseFlags.WheeledUp) && isSamePos)
        {
            resultsView.Scroll(-1);
            return true;
        }

        if (Build.IsWindows)
        {
            if (mouses.TryGetValue(ev.Flags, out var callback))
            {
                callback(ev.X, ev.Y);
                return true;
            }
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
        statusLabel.Text = Text.Add(repoInfo).White($" {commit.Sid}").Color(color, $" ({branch.NiceNameUnique})");
    }


    Text GetRepoInfo()
    {
        var commitCount = currentRepo.Commits.Count(c => c.BranchName != "<none>");
        var branchCount = currentRepo.Commits.Select(c => c.BranchPrimaryName).Where(b => b != "<none>").Distinct().Count();
        return Text.Dark($"{commitCount} commits, {branchCount} branches,");
    }
}




