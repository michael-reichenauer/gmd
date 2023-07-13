using gmd.Cui.Common;
using gmd.Server;
using Terminal.Gui;

namespace gmd.Cui;

interface IFilterDlg
{
    void Show(Server.Repo repo, Action<Server.Repo> onRepoChanged);
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


    internal FilterDlg(IServer server)
    {
        this.server = server;
    }


    public void Show(Server.Repo repo, Action<Server.Repo> onRepoChanged)
    {
        this.orgRepo = repo;
        this.currentFilter = null!;
        this.onRepoChanged = onRepoChanged;

        dlg = new UIDialog("Filter Commits", Dim.Fill() + 1, 3, null, options => { options.X = -1; options.Y = -1; });

        filterField = dlg.AddTextField(1, 0, 40);
        filterField.KeyUp += (k) => OnKeyUp(k);    // Update results and select commit on keys

        resultCountField = dlg.AddLabel(44, 0);

        // Initializes results with current repo commits
        UI.Post(() => UpdateFilteredResults().RunInBackground());

        dlg.Show(filterField);
    }


    async Task UpdateFilteredResults()
    {
        var filter = filterField.Text.Trim();
        if (filter == currentFilter) return;

        currentFilter = filter;
        if (filter == "")
        {
            resultCountField.Text = orgRepo.Commits.Count == 1 ? $"1 result" : $"{orgRepo.Commits.Count} results";
            onRepoChanged(orgRepo);
            return;
        }

        if (Try(out var filteredRepo, out var e, await server.GetFilteredRepoAsync(orgRepo, filter, 1000)))
        {
            var commits = filteredRepo.Commits;
            resultCountField.Text = filteredRepo.Commits.Count == 1 ? $"1 result" : $"{filteredRepo.Commits.Count} results";
            onRepoChanged(filteredRepo);
        }
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
        // if (filteredCommits.Count > 0)
        // {   // User selected commit from list
        //     this.selectedCommit = filteredCommits[index];
        // }

        dlg.Close();
    }
}




