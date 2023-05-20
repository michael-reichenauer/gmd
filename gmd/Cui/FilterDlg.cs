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

        var width = 86;
        var dlg = new UIDialog("Filter Commits", width, 20, null, options =>
        {
            options.ColorScheme.Focus = TextColor.White;
            options.ColorScheme.Normal = TextColor.BrightMagenta;
            options.Y = 0;
        });

        var contentView = dlg.AddContentView(0, 2, Dim.Fill(), Dim.Fill(), OnGetContent);

        var filter = dlg.AddTextField(1, 0, 40, "");
        filter.KeyUp += (k) => OnKeyUp(k, filter, contentView);

        dlg.Show(filter);

        if (commit == null) return R.Error();

        return commit;
    }


    void OnKeyUp(View.KeyEventEventArgs e, UITextField filter, ContentView contentView)
    {
        try
        {
            var key = e.KeyEvent.Key;
            Log.Info($"OnKeyUp: {key} - {filter.Text}");
            if (key == Key.CursorDown)
            {
                contentView.Move(1);
                return;
            }
            if (key == Key.CursorUp)
            {
                contentView.Move(-1);
                return;
            }
            if (key == Key.Enter)
            {
                OnEnter(contentView.CurrentIndex);
                return;
            }
            if (filter.Text == currentFilter)
            {
                return;
            }
            if (filter.Text.Length < 2)
            {
                commits = new List<Server.Commit>();
                contentView.TriggerUpdateContent(commits.Count);
                return;
            }

            commits = server.GetFilterCommits(repo!.Repo, filter.Text);
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


    IEnumerable<Text> OnGetContent(int firstIndex, int count, int currentIndex, int width) =>
        commits.Skip(firstIndex).Take(count).Select(c => Text.New
            .White($"{c.Subject.Max(50),-50}")
            .Dark($" {c.Sid} {c.Author.Max(15),-15} {c.AuthorTime.ToString("yy-MM-dd")}"));
}




