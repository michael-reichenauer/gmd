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

    TextField? nameField;
    ContentView? contentView;

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

        nameField = Components.TextField(1, 0, 30, "");
        Label sep1 = Components.TextIndicator(nameField);

        contentView = new ContentView(OnGetContent)
        { X = 0, Y = 2, Width = Dim.Fill(), Height = Dim.Fill() };
        contentView.RegisterKeyHandler(Key.Enter, () => OnEnter());

        var width = 85;

        var dialog = Components.Dialog("Search/Filter", width, 20);
        dialog.Y = 0;
        dialog.Closed += e => UI.HideCursor();
        dialog.Add(nameField, sep1, contentView);

        nameField.KeyUp += (k) => OnKeyUp();

        nameField.SetFocus();
        UI.ShowCursor();
        UI.RunDialog(dialog);

        if (commit == null)
        {
            return R.Error();
        }

        return commit;
    }

    string Filter() => nameField!.Text.ToString()?.Trim() ?? "";


    void OnEnter()
    {
        if (commits.Count == 0)
        {
            Application.RequestStop();
            return;
        }
        this.commit = commits[contentView!.CurrentIndex];

        Application.RequestStop();
    }

    void OnKeyUp()
    {
        string filter = Filter();
        if (filter == currentFilter)
        {
            return;
        }
        if (filter.Length < 2)
        {
            commits = new List<Server.Commit>();
            contentView!.TriggerUpdateContent(commits.Count);
            return;
        }

        commits = server.GetFilterCommits(repo!.Repo, filter);
        contentView!.TriggerUpdateContent(commits.Count);
    }


    IEnumerable<Text> OnGetContent(int firstIndex, int count, int currentIndex, int width) =>
        commits.Skip(firstIndex).Take(count).Select(c => Text.New
            .White($"{c.Subject.Max(50),-50}")
            .Dark($"{c.Sid} {c.Author.Max(15),-15} {c.AuthorTime.ToString("yy-MM-dd")}"));
}




