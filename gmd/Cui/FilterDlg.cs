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
    private readonly IServer server;

    IReadOnlyList<Server.Commit> commits = new List<Server.Commit>();

    string currentFilter = "";
    IRepo? repo;
    Server.Commit? commit;

    internal FilterDlg(IServer server)
    {
        this.server = server;
    }

    public R<Server.Commit> Show(IRepo repo)
    {
        this.repo = repo;

        //var width = 86;
        var dlg = new UIDialog("Filter Commits", Dim.Fill(), 20, null, options =>
        {
            options.ColorScheme.Focus = TextColor.White;
            options.ColorScheme.Normal = TextColor.BrightMagenta;
            options.Y = 0;
        });

        var resultsView = dlg.AddContentView(0, 2, Dim.Fill(), Dim.Fill(), OnGetContent);
        resultsView.RegisterKeyHandler(Key.Esc, () => dlg.Close());
        resultsView.IsNoCursor = false;
        resultsView.IsCursorMargin = true;

        var filterField = dlg.AddTextField(1, 0, 40, "");
        filterField.KeyUp += (k) => OnKeyUp(k, filterField, resultsView);

        dlg.Show(filterField);

        if (commit == null) return R.Error();

        return commit;
    }


    void OnKeyUp(View.KeyEventEventArgs e, UITextField filter, ContentView contentView)
    {
        try
        {
            var key = e.KeyEvent.Key;
            if (key == Key.Enter)
            {
                OnEnter(contentView.CurrentIndex);
                return;
            }
            var filterText = filter.Text.Trim();
            if (filterText == currentFilter)
            {
                return;
            }
            currentFilter = filterText;
            if (filterText.Length < 2 && filterText != "$")
            {
                commits = new List<Server.Commit>();
                contentView.TriggerUpdateContent(commits.Count);
                return;
            }

            commits = server.GetFilterCommits(repo!.Repo, filterText);
            contentView!.TriggerUpdateContent(commits.Count);
        }
        finally
        {
            e.Handled = true;
        }

    }


    void OnEnter(int index)
    {
        if (commits.Count == 0)
        {
            Application.RequestStop();
            return;
        }
        this.commit = commits[index];

        Application.RequestStop();
    }


    IEnumerable<Text> OnGetContent(int firstIndex, int count, int currentIndex, int width)
    {
        if (commits.Count == 0)
        {
            var msg = currentFilter.Length < 2
                ? "Please enter at least 2 characters, space to separate conditions and " +
                  "quotes for exact matches"
                : "No matching commits";
            return new[] { Text.New.Dark(msg) };
        }

        return commits.Skip(firstIndex).Take(count).Select((c, i) =>
        {
            var sidAuthDate = $"{c.Sid} {c.Author.Max(10),-10} {c.AuthorTime.ToString("yy-MM-dd")}";
            var branchName = $"({ToShortBranchName(c.BranchViewName)})";
            var tags = c.Tags.Count > 0 ? $"[{string.Join("][", c.Tags.Select(t => t.Name))}]".Max(20) : "";

            var subjectLength = width - sidAuthDate.Length - branchName.Length - tags.Length - 1;
            var subject = c.Subject.Max(subjectLength, true);

            return (i == currentIndex - firstIndex
                ? Text.New.WhiteSelected($"{subject} {branchName}{tags} {sidAuthDate}")
                : Text.New.White($"{subject}").Dark(branchName).Green(tags).Dark($" {sidAuthDate}"));
        });
    }

    string ToShortBranchName(string branchName)
    {
        if (branchName.Length > 16)
        {   // Branch name to long, shorten it
            branchName = "┅" + branchName.Substring(branchName.Length - 16);
        }
        return branchName;
    }
}




