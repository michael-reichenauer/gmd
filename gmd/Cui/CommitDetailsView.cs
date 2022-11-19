using gmd.Cui.Common;
using Terminal.Gui;

namespace gmd.Cui;


interface ICommitDetailsView
{
    View View { get; }
}

class CommitDetailsView : ICommitDetailsView
{
    ContentView? contentView;
    Server.Commit? commit;
    IReadOnlyList<string> rows = new List<string>();


    public CommitDetailsView()
    {
        contentView = new ContentView(onDrawContent)
        {
            X = 0,
            Y = 0,
            Width = 0,
            Height = 0,
            // Width = Dim.Fill(),
            // Height = Dim.Fill(),
        };
    }

    public View View => contentView!;

    public int TotalRows => rows.Count;

    public void Set(Server.Commit commit) => UI.RunInBackground(async () =>
    {
        await Task.Yield();

        this.commit = commit;
        rows = new List<string>() { commit.Id };

        contentView!.TriggerUpdateContent(0);
    });


    void onDrawContent(int firstIndex, int count, int currentIndex, int width)
    {
        if (commit == null)
        {
            return;
        }

        DrawRows(firstIndex, count, width);
    }

    void DrawRows(int firstRow, int rowCount, int contentWidth)
    {
        int x = contentView!.ContentX;
        for (int y = 0; y < rowCount && y + firstRow < TotalRows; y++)
        {
            var row = rows[firstRow + y];
            Text.New.White($"{row}")
                .Draw(contentView!, x, y);
        }
    }
}

