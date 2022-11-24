using gmd.Cui.Common;
using Terminal.Gui;


namespace gmd.Cui;

interface IDiffView
{
    void Show(Server.CommitDiff diff, string commitId);
    void Show(Server.CommitDiff[] diffs, string commitId = "");
}


class DiffView : IDiffView
{
    static readonly Text splitLine = Text.New.Dark("│");

    readonly IDiffConverter diffService;

    ContentView? contentView;
    Toplevel? diffView;
    DiffRows diffRows = new DiffRows();
    int rowStartX = 0;
    string commitId = "";


    public DiffView(IDiffConverter diffService)
    {
        this.diffService = diffService;
    }


    public void Show(Server.CommitDiff diff, string commitId) => Show(new[] { diff }, commitId);

    public void Show(Server.CommitDiff[] diffs, string commitId)
    {
        this.commitId = commitId;

        diffView = new Toplevel() { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), };
        contentView = new ContentView(OnGetContent)
        { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), IsNoCursor = true, };

        diffView.Add(contentView);

        RegisterShortcuts(contentView);

        diffRows = diffService.ToDiffRows(diffs);
        contentView.TriggerUpdateContent(diffRows.Rows.Count);

        UI.RunDialog(diffView);
    }


    void RegisterShortcuts(ContentView view)
    {
        view.RegisterKeyHandler(Key.CursorRight, OnRightArrow);
        view.RegisterKeyHandler(Key.CursorLeft, OnLeftArrow);
    }


    private void OnLeftArrow()
    {
        if (rowStartX > 0)
        {
            rowStartX--;
            contentView?.TriggerUpdateContent(diffRows!.Count);
        }
    }


    private void OnRightArrow()
    {
        int maxColumnWidth = contentView!.ContentWidth / 2;
        if (diffRows!.MaxLength - rowStartX > maxColumnWidth)
        {
            rowStartX++;
            contentView!.TriggerUpdateContent(diffRows!.Count);
        }
    }


    IEnumerable<Text> OnGetContent(int firstRow, int rowCount, int rowStartX, int contentWidth)
    {
        int columnWidth = (contentWidth - 1) / 2;
        int oneColumnWidth = columnWidth * 2 + 1;

        return diffRows.Rows.Skip(firstRow).Take(rowCount)
            .Select(r => ToText(r, columnWidth, oneColumnWidth));
    }


    private Text ToText(DiffRow row, int columnWidth, int oneColumnWidth) => row.Mode switch
    {
        DiffRowMode.Line => row.Left.AsLine(oneColumnWidth),
        DiffRowMode.SpanBoth => row.Left.Subtext(0, oneColumnWidth),
        DiffRowMode.LeftRight => Text.New
            .Add(row.Left.Subtext(rowStartX, columnWidth, true))
            .Add(splitLine)
            .Add(row.Right.Subtext(rowStartX, columnWidth, true)),
        _ => throw Asserter.FailFast($"Unknown row mode {row.Mode}")
    };
}
