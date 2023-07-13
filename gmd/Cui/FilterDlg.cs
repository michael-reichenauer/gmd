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
    const int MaxResults = 1000;
    private readonly IServer server;

    UIDialog dlg = null!;
    UITextField filterField = null!;
    Label resultCountField = null!;

    Server.Repo orgRepo = null!;
    Server.Repo currentRepo = null!;

    string currentFilter = null!;
    Action<Repo> onRepoChanged = null!;
    ContentView resultsView = null!;
    R<Server.Commit> selectedCommit = R.Error("No commit selected");

    internal FilterDlg(IServer server)
    {
        this.server = server;
    }


    public R<Server.Commit> Show(Server.Repo repo, Action<Server.Repo> onRepoChanged, ContentView commitsView)
    {
        this.orgRepo = repo;
        this.currentFilter = null!;
        this.onRepoChanged = onRepoChanged;
        this.resultsView = commitsView;

        dlg = new UIDialog("Filter Commits", Dim.Fill() + 1, 3, OnKey, options => { options.X = -1; options.Y = -1; });

        dlg.AddLabel(0, 0, "Search:");
        filterField = dlg.AddTextField(9, 0, 40);
        filterField.KeyUp += (k) => OnKeyUp(k);    // Update results and select commit on keys

        resultCountField = dlg.AddLabel(53, 0);

        // Initializes results with current repo commits
        UI.Post(() => UpdateFilteredResults().RunInBackground());

        dlg.Show(filterField);

        return selectedCommit;
    }

    private bool OnKey(Key key)
    {
        Log.Info($"FilterDlg.OnKey: {key}");
        switch (key)
        {
            case Key.Enter:
                OnEnter(resultsView.CurrentIndex);
                return true;
            case Key.CursorUp:
                resultsView.Move(-1);
                return true;
            case Key.CursorDown:
                resultsView.Move(1);
                return true;

        }
        return false;
    }

    async Task UpdateFilteredResults()
    {
        var filter = filterField.Text.Trim();
        if (filter == currentFilter) return;

        currentFilter = filter;
        currentRepo = orgRepo;
        var count = orgRepo.Commits.Count;

        if (filter != "" && Try(out var filteredRepo, out var e, await server.GetFilteredRepoAsync(orgRepo, filter, 1000)))
        {
            currentRepo = filteredRepo;
            count = currentRepo.Commits.Count(c => c.BranchName != "<none>");
            resultsView.MoveToTop();
        }

        resultCountField.Text = count == 1 ? $"1 result" : $"{count} results";
        onRepoChanged(currentRepo);
    }


    // User pressed key in filter field, update results 
    void OnKeyUp(View.KeyEventEventArgs e)
    {
        UpdateFilteredResults().RunInBackground();
        e.Handled = true;
    }

    // User selected commit from list (or pressed enter on empty results to just close dlg)
    void OnEnter(int index)
    {
        Log.Info($"OnEnter: {index}");
        if (currentRepo.Commits.Count > 0)
        {   // User selected commit from list
            this.selectedCommit = currentRepo.Commits[index];
        }

        dlg.Close();
    }
}




