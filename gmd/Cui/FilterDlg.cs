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
    Label resultCountField = null!;
    Label commitLabel = null!;
    Label branchLabel = null!;

    Action<Repo> onRepoChanged = null!;
    Server.Repo orgRepo = null!;
    Server.Repo currentRepo = null!;
    string currentFilter = null!;
    ContentView resultsView = null!;
    R<Server.Commit> selectedCommit = R.Error("No commit selected");


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

        dlg.AddLabel(0, 0, "Search:");
        filterField = dlg.AddTextField(9, 0, 40);
        filterField.KeyUp += (k) => OnFilterFieldKeyUp(k);    // Update results and select commit on keys

        // Status fields
        resultCountField = dlg.AddLabel(53, 0);
        commitLabel = dlg.AddLabel(67, 0, "");
        branchLabel = dlg.AddLabel(74, 0, "");
        branchLabel.ColorScheme = new ColorScheme() { Normal = TextColor.White };

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
                resultsView.Move(-resultsView.Count);
                return true;
            case Key.End:
                resultsView.Move(resultsView.Count);
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

        ShowRepoInfo();
        ShowCommitInfo();
        onRepoChanged(currentRepo);
    }


    void ShowCommitInfo()
    {
        var index = resultsView.CurrentIndex;
        if (currentRepo.Commits.Count == 0 || index >= currentRepo.Commits.Count)
        {
            commitLabel.Text = "";
            branchLabel.Text = "";
            return;
        };

        var commit = currentRepo.Commits[index];
        var branch = currentRepo.BranchByName[commit.BranchName];
        var color = branchColorService.GetColor(currentRepo, branch);
        commitLabel.Text = commit.Sid;
        branchLabel.Text = $"({branch.NiceNameUnique})";
        branchLabel.ColorScheme.Normal = color;
    }


    void ShowRepoInfo()
    {
        var count = currentRepo.Commits.Count(c => c.BranchName != "<none>");
        resultCountField.Text = count == 1 ? $"1 result" : $"{count} commits,";
    }
}




