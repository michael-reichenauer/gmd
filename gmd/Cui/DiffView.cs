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
    readonly IViewRepoService viewRepoService;
    readonly IDiffService diffService;
    static readonly Text splitLine = Text.New.DarkGray("│");

    Toplevel diffView;
    int rowStartIndex = 0;
    Repo? repo;
    string commitId = "";

    public Toplevel View => diffView;

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

        contentView = new ContentView(onDrawDiffContent)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            IsNoCursor = true,
        };

        diffView.Add(contentView);
        this.viewRepoService = viewRepoService;
        this.diffService = diffService;

        RegisterKeyHandlers();
    }

    void RegisterKeyHandlers()
    {
        contentView.RegisterKeyHandler(Key.CursorRight, OnRightArrow);
        contentView.RegisterKeyHandler(Key.CursorLeft, OnLeftArrow);
        contentView.RegisterKeyHandler(Key.r, Refresh);
        contentView.RegisterKeyHandler(Key.R, Refresh);
    }

    private void Refresh()
    {
        ShowAsync(repo!, commitId).RunInBackground();
    }

    private void OnLeftArrow()
    {
        if (rowStartIndex > 0)
        {
            rowStartIndex--;
            contentView.TriggerUpdateContent(diffRows!.Count);
        }
    }

    private void OnRightArrow()
    {
        int maxColumnWidth = contentView.ContentWidth / 2;
        if (diffRows!.MaxLength - rowStartIndex > maxColumnWidth)
        {
            rowStartIndex++;
            contentView.TriggerUpdateContent(diffRows!.Count);
        }
    }

    public void Show(Repo repo, string commitId)
    {
        this.repo = repo;
        this.commitId = commitId;

        ShowAsync(repo, commitId).RunInBackground();
        Application.Run(diffView);
    }

    async Task ShowAsync(Repo repo, string commitId)
    {
        if (!Try(out var diff, out var e,
            commitId == Repo.UncommittedId
                ? await viewRepoService.GetUncommittedDiff(repo)
                : await viewRepoService.GetCommitDiffAsync(repo, commitId)))
        {
            UI.ErrorMessage($"Failed to get diff:\n{e}");
            return;
        }

        diffRows = diffService.CreateRows(diff);
        contentView.TriggerUpdateContent(TotalRows);
    }

    void onDrawDiffContent(Rect bounds, int firstIndex, int currentIndex)
    {
        if (diffRows == null)
        {
            return;
        }

        int firstRow = Math.Min(firstIndex, TotalRows);
        int rowCount = Math.Min(bounds.Height, TotalRows - firstRow);

        if (rowCount == 0 || bounds.Width == 0)
        {
            return;
        };

        Rect contentRect = new Rect(0, firstRow, 50, rowCount);

        int contentWidth = bounds.Width;
        int rowX = contentRect.X;
        DrawDiffRows(firstRow, rowCount, rowStartIndex, contentWidth);
    }

    void DrawDiffRows(int firstRow, int rowCount, int rowStart, int contentWidth)
    {
        int columnWidth = (contentWidth - 1) / 2;
        int oneColumnWidth = columnWidth * 2 + 1;
        for (int y = 0; y < rowCount; y++)
        {
            var row = diffRows!.Rows[firstRow + y];
            if (row.Mode == DiffRowMode.Line)
            {
                row.Left.DrawAsLine(contentView, contentView.ContentX, y, oneColumnWidth);
            }
            else if (row.Mode == DiffRowMode.SpanBoth)
            {
                row.Left.Draw(contentView, contentView.ContentX, y, 0, oneColumnWidth);
            }
            else if (row.Mode == DiffRowMode.LeftRight)
            {
                row.Left.Draw(contentView, contentView.ContentX, y, rowStart, columnWidth);
                splitLine.Draw(contentView, contentView.ContentX + columnWidth, y);
                row.Right.Draw(contentView, contentView.ContentX + columnWidth + 1, y, rowStart, columnWidth);
            }
        }
    }
}
