using gmd.Cui.Common;
using gmd.Server;
using Terminal.Gui;

namespace gmd.Cui;

interface IFilterDlg
{
    R<Server.Commit> Show(IRepo repo);
}

class FilterDlg : IFilterDlg
{
    const int MaxResults = 1000;
    private readonly IServer server;

    IReadOnlyList<Server.Commit> filteredCommits = new List<Server.Commit>();

    IRepo repo = null!;
    UIDialog dlg = null!;
    UITextField filterField = null!;
    Label resultCountField = null!;
    ContentView resultsView = null!;
    R<Server.Commit> selectedCommit = R.Error();
    string currentFilter = null!;


    internal FilterDlg(IServer server)
    {
        this.server = server;
    }


    public R<Server.Commit> Show(IRepo repo)
    {
        this.repo = repo;

        dlg = new UIDialog("Filter Commits", Dim.Fill(), 20, null, options => { options.Y = 0; });

        filterField = dlg.AddTextField(1, 0, 40);
        filterField.KeyUp += (k) => OnKeyUp(k);    // Update results and select commit on keys

        resultCountField = dlg.AddLabel(44, 0);

        // Filtered results
        resultsView = dlg.AddContentView(0, 2, Dim.Fill(), Dim.Fill(), OnGetContent);
        resultsView.RegisterKeyHandler(Key.Esc, () => dlg.Close());
        resultsView.IsShowCursor = true;
        resultsView.IsCursorMargin = true;

        // Initializes results with current repo commits
        UI.Post(() => UpdateFilterdResults());

        dlg.Show(filterField);
        return selectedCommit;
    }


    void UpdateFilterdResults()
    {
        var filter = filterField.Text.Trim();
        if (filter == currentFilter) return;

        currentFilter = filter;
        var commits = server.GetFilterCommits(repo!.Repo, filter, MaxResults);
        resultsView!.TriggerUpdateContent(commits.Count);

        this.filteredCommits = commits;
        resultCountField.Text = commits.Count == 1 ? $"1 result" : $"{commits.Count} results";
    }

    // User pressed key in filter field, select commit on enter or update results 
    void OnKeyUp(View.KeyEventEventArgs e)
    {
        try
        {
            var key = e.KeyEvent.Key;
            if (key == Key.Enter)
            {
                OnEnter(resultsView.CurrentIndex);
                return;
            }

            UpdateFilterdResults();
        }
        finally
        {
            e.Handled = true;
        }
    }

    // User selected commit from list (or pressed enter on empty results to just close dlg)
    void OnEnter(int index)
    {
        if (filteredCommits.Count > 0)
        {   // User selected commit from list
            this.selectedCommit = filteredCommits[index];
        }

        dlg.Close();
    }


    // Show results in list or empyt result message,
    IEnumerable<Text> OnGetContent(int firstIndex, int count, int currentIndex, int width)
    {
        if (filteredCommits.Count == 0) return new[] { Text.New.Dark("No matching commits") };

        return filteredCommits.Skip(firstIndex).Take(count).Select((c, i) =>
        {
            if (firstIndex + i >= MaxResults - 1) return Text.New.Dark("... <To many results, please adjust filter>");

            // Calculate commit row text
            var sidAuthDate = $"{c.Sid} {c.Author.Max(10),-10} {c.AuthorTime.ToString("yy-MM-dd")}";
            var branchName = $"({ToShortName(c.BranchViewName)})";
            var tags = c.Tags.Count > 0 ? $"[{string.Join("][", c.Tags.Select(t => t.Name))}]".Max(20) : "";
            var subjectLength = width - sidAuthDate.Length - branchName.Length - tags.Length - 2;
            var subject = c.Subject.Max(subjectLength, true);

            // Show selected or unselected commit row 
            return (i == currentIndex - firstIndex
                ? Text.New.WhiteSelected($"{subject} {branchName}{tags} {sidAuthDate}")
                : Text.New.White($"{subject} ").Dark(branchName).Green(tags).Dark($" {sidAuthDate}"));
        });
    }

    string ToShortName(string branchName)
    {
        if (branchName.Length > 16)
        {   // Branch name to long, shorten it (show last 16 chars)
            branchName = "┅" + branchName.Substring(branchName.Length - 16);
        }
        return branchName;
    }
}




