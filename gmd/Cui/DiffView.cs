using gmd.Server;
using Terminal.Gui;

namespace gmd.Cui;

interface IDiffView
{
    void ShowCurrentRow();
    void ShowUncommittedDiff();
}


class DiffView : IDiffView
{
    static readonly Text splitLine = Text.New.Dark("│");

    readonly IServer viewRepoService;
    readonly IDiffConverter diffService;

    readonly IRepo repo;

    ContentView contentView;
    Toplevel diffView;

    int rowStartIndex = 0;
    string commitId = "";


    DiffRows? diffRows = null;

    int TotalRows => diffRows?.Count ?? 0;

    public DiffView(IServer viewRepoService, IDiffConverter diffService, IRepo repo)
    {
        this.repo = repo;
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


    public void ShowCurrentRow() => Show(repo.CurrentIndexCommit.Id);

    public void ShowUncommittedDiff() => Show(Repo.UncommittedId);


    void Show(string commitId)
    {
        this.commitId = commitId;
        ShowAsync(commitId).RunInBackground();

        UI.RunDialog(diffView);
    }


    void RegisterKeyHandlers()
    {
        contentView.RegisterKeyHandler(Key.CursorRight, OnRightArrow);
        contentView.RegisterKeyHandler(Key.CursorLeft, OnLeftArrow);
        contentView.RegisterKeyHandler(Key.r, OnRefresh);
        contentView.RegisterKeyHandler(Key.R, OnRefresh);
    }

    private void OnRefresh() => ShowAsync(commitId).RunInBackground();

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

    async Task ShowAsync(string commitId)
    {
        var t = Timing.Start;

        var diffTask = commitId == Repo.UncommittedId
            ? viewRepoService.GetUncommittedDiff(repo.Repo.Path)
            : viewRepoService.GetCommitDiffAsync(commitId, repo.Repo.Path);

        if (!Try(out var diff, out var e, await diffTask))
        {
            UI.ErrorMessage($"Failed to get diff:\n{e}");
            return;
        }

        diffRows = diffService.ToDiffRows(diff);
        Log.Info($"{t} {diffRows}");
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
        for (int y = 0; y < rowCount && y + firstRow < diffRows!.Rows.Count; y++)
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
