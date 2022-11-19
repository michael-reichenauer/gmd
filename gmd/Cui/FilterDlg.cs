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
    int rowStartIndex = 0;

    int TotalRows => commits.Count;
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

        nameField = new TextField(1, 0, 30, "");
        Label sep1 = new Label(nameField.Frame.X - 1, nameField.Frame.Y + 1,
            "└" + new string('─', nameField.Frame.Width) + "┘");

        contentView = new ContentView(onDrawContent)
        { X = 0, Y = 2, Width = Dim.Fill(), Height = Dim.Fill() };
        contentView.RegisterKeyHandler(Key.Enter, () => OnEnter());

        var width = 85;

        var dialog = new Dialog("Search/Filter", width, 20)
        {
            Border = { Effect3D = false, BorderStyle = BorderStyle.Rounded },
            ColorScheme = ColorSchemes.DialogColorScheme,
            Y = 0,
        };
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

    private void OnEnter()
    {
        if (TotalRows == 0)
        {
            Application.RequestStop();
            return;
        }
        this.commit = commits[contentView!.CurrentIndex];

        Application.RequestStop();
    }

    private void OnKeyUp()
    {
        string filter = Filter();
        if (filter == currentFilter)
        {
            return;
        }
        if (filter.Length < 2)
        {
            commits = new List<Server.Commit>();
            contentView!.TriggerUpdateContent(TotalRows);
            return;
        }

        commits = server.GetFilterCommits(repo!.Repo, filter);
        contentView!.TriggerUpdateContent(TotalRows);
    }

    private string Filter() => nameField!.Text.ToString()?.Trim() ?? "";

    void onDrawContent(int firstIndex, int count, int currentIndex, int width)
    {
        if (commits == null)
        {
            return;
        }

        Rect contentRect = new Rect(0, firstIndex, 50, count);

        DrawDiffRows(firstIndex, count);
    }

    void DrawDiffRows(int firstRow, int rowCount)
    {
        int x = contentView!.ContentX;
        for (int y = 0; y < rowCount && y + firstRow < commits.Count; y++)
        {
            var c = commits[firstRow + y];
            Text.New.White($"{c.Subject.Max(50),-50}")
                .Dark($"{c.Sid} {c.Author.Max(15),-15} {c.AuthorTime.ToString("yy-MM-dd")}")
                .Draw(contentView!, x, y);
        }
    }
}




