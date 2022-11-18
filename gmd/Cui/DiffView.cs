using gmd.Cui.Common;
using Terminal.Gui;


namespace gmd.Cui;

interface IDiffView
{
    void Show(Server.CommitDiff diff, string commitId);
}


class DiffView : IDiffView
{
    static readonly Text splitLine = Text.New.Dark("│");

    readonly IDiffConverter diffService;

    ContentView? contentView;
    Toplevel? diffView;

    int rowStartIndex = 0;
    string commitId = "";


    DiffRows? diffRows = null;

    int TotalRows => diffRows?.Count ?? 0;

    public DiffView(IDiffConverter diffService)
    {
        this.diffService = diffService;
    }

    public void Show(Server.CommitDiff diff, string commitId)
    {
        this.commitId = commitId;

        diffView = new Toplevel() { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), };
        contentView = new ContentView(onDrawContent)
        { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), IsNoCursor = true, };

        diffView.Add(contentView);

        RegisterShortcuts(contentView);

        diffRows = diffService.ToDiffRows(diff);
        contentView.TriggerUpdateContent(TotalRows);

        UI.RunDialog(diffView);
    }


    void RegisterShortcuts(ContentView view)
    {
        view.RegisterKeyHandler(Key.CursorRight, OnRightArrow);
        view.RegisterKeyHandler(Key.CursorLeft, OnLeftArrow);
        // view.RegisterKeyHandler(Key.r, () => {repo!.UpdateDiff(commitId);});
        // view.RegisterKeyHandler(Key.R, () => repo!.UpdateDiff(commitId));
    }


    private void OnLeftArrow()
    {
        if (rowStartIndex > 0)
        {
            rowStartIndex--;
            contentView?.TriggerUpdateContent(diffRows!.Count);
        }
    }


    private void OnRightArrow()
    {
        int maxColumnWidth = contentView!.ContentWidth / 2;
        if (diffRows!.MaxLength - rowStartIndex > maxColumnWidth)
        {
            rowStartIndex++;
            contentView!.TriggerUpdateContent(diffRows!.Count);
        }
    }


    void onDrawContent(Rect bounds, int firstIndex, int currentIndex)
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
                row.Left.DrawAsLine(contentView!, contentView!.ContentX, y, oneColumnWidth);
            }
            else if (row.Mode == DiffRowMode.SpanBoth)
            {
                row.Left.Draw(contentView!, contentView!.ContentX, y, 0, oneColumnWidth);
            }
            else if (row.Mode == DiffRowMode.LeftRight)
            {
                row.Left.Draw(contentView!, contentView!.ContentX, y, rowStart, columnWidth);
                splitLine.Draw(contentView, contentView.ContentX + columnWidth, y);
                row.Right.Draw(contentView, contentView.ContentX + columnWidth + 1, y, rowStart, columnWidth);
            }
        }
    }
}
