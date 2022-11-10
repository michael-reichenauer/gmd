using gmd.ViewRepos;
using Terminal.Gui;

namespace gmd.Cui;

interface IDiffView
{
    Toplevel View { get; }

    void Show(Repo repo, string commitId);
}


class DiffView : IDiffView
{
    private readonly IViewRepoService viewRepoService;
    private readonly IDiffService diffService;
    static readonly Text splitLine = Text.New.DarkGray("│");

    public Toplevel diffView { get; }

    readonly ContentView contentView;

    DiffRows? diffRows = null;

    int TotalRows => diffRows?.Count ?? 0;

    public DiffView(IViewRepoService viewRepoService, IDiffService diffService)
    {
        diffView = new Toplevel()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        contentView = new ContentView(onDrawRepoContent)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        diffView.Add(contentView);
        this.viewRepoService = viewRepoService;
        this.diffService = diffService;
    }

    public void Show(Repo repo, string commitId)
    {
        ShowAsync(repo, commitId).RunInBackground();
        Application.Run(diffView);
    }

    async Task ShowAsync(Repo repo, string commitId)
    {
        if (commitId == Repo.UncommittedId)
        {
            if (!Try(out var diff, out var e, await viewRepoService.GetUncommittedDiff(repo)))
            {
                UI.ErrorMessage($"Failed to get uncommitted diff:\n{e}");
                return;
            }

            diffRows = diffService.CreateRows(diff);
            Log.Info($"Diff rows {diffRows.Count}");

            contentView.TriggerUpdateContent(TotalRows);
        }
    }

    private void onDrawRepoContent(Rect bounds, int firstIndex, int currentIndex)
    {
        Log.Info("Draw diff content");
        if (diffRows == null)
        {
            Log.Info("Nothing to drawt");
            return;
        }

        int firstRow = Math.Min(firstIndex, TotalRows);
        int rowCount = Math.Min(bounds.Height, TotalRows - firstRow);


        if (rowCount == 0 || bounds.Width == 0)
        {
            Log.Info("Empty lines");
            return;
        };

        Rect contentRect = new Rect(0, firstRow, 50, rowCount);

        int rowWidth = 100;
        int rowX = contentRect.X;

        Log.Info($"Drawing lines first: {firstRow}, count: {rowCount}");
        for (int y = firstRow; y < firstRow + rowCount; y++)
        {
            var row = diffRows.Rows[y];
            if (row.Mode == DiffRowMode.Line)
            {
                row.Left.DrawAsLine(contentView, contentView.ContentX, y, rowWidth);
            }
            else if (row.Mode == DiffRowMode.Left)
            {
                row.Left.Draw(contentView, contentView.ContentX, y, 0, rowWidth);
            }
            else if (row.Mode == DiffRowMode.LeftRight)
            {
                row.Left.Draw(contentView, contentView.ContentX, y, 0, rowWidth);
                splitLine.Draw(contentView, contentView.ContentX + rowWidth, y);
                row.Right.Draw(contentView, contentView.ContentX + rowWidth + 1, y, 0, rowWidth);
            }
        }
    }


    public Toplevel View => diffView;
}
