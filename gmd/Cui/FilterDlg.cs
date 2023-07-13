using gmd.Cui.Common;
using gmd.Server;
using Terminal.Gui;

namespace gmd.Cui;

interface IFilterDlg
{
    void Show(Server.Repo repo, Action<Server.Repo> onRepoChanged, ContentView commitsView);
}

class FilterDlg : IFilterDlg
{
    const int MaxResults = 1000;
    private readonly IServer server;

    UIDialog dlg = null!;
    UITextField filterField = null!;
    Label resultCountField = null!;

    Server.Repo orgRepo = null!;
    string currentFilter = null!;
    Action<Repo> onRepoChanged = null!;
    private ContentView resultsView = null!;

    internal FilterDlg(IServer server)
    {
        this.server = server;
    }


    public void Show(Server.Repo repo, Action<Server.Repo> onRepoChanged, ContentView commitsView)
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
        var repo = orgRepo;
        var count = orgRepo.Commits.Count;

        if (filter != "" && Try(out var filteredRepo, out var e, await server.GetFilteredRepoAsync(orgRepo, filter, 1000)))
        {
            repo = filteredRepo;
            count = repo.Commits.Count(c => c.BranchName != "<none>");
        }

        resultCountField.Text = count == 1 ? $"1 result" : $"{count} results";
        onRepoChanged(repo);
    }


    // User pressed key in filter field, select commit on enter or update results 
    void OnKeyUp(View.KeyEventEventArgs e)
    {
        try
        {
            var key = e.KeyEvent.Key;
            if (key == Key.Enter)
            {
                //OnEnter(resultsView.CurrentIndex);
                return;
            }

            UpdateFilteredResults().RunInBackground();
        }
        finally
        {
            e.Handled = true;
        }
    }

    // User selected commit from list (or pressed enter on empty results to just close dlg)
    void OnEnter(int index)
    {
        Log.Info($"OnEnter: {index}");
        // if (filteredCommits.Count > 0)
        // {   // User selected commit from list
        //     this.selectedCommit = filteredCommits[index];
        // }

        dlg.Close();
    }
}




