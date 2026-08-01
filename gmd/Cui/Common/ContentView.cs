using System.Text;
using Terminal.Gui;

namespace gmd.Cui.Common;

internal delegate (IEnumerable<Text> rows, int total) GetContentCallback(
    int firstIndex,
    int count,
    int currentIndex,
    int contentWidth
);
internal delegate void OnKeyCallback();
internal delegate bool OnKeyCallbackReturn();
internal delegate void OnMouseCallback(int x, int y);
internal delegate bool OnMouseCallbackReturn(int x, int y);

// A scrollable list of rows, which is what most of the gmd UI is drawn in: the log view, the diff
// view, the menus and several dialogs. The rows are either handed to the constructor or fetched
// while drawing through a GetContentCallback, so that a large repo is never materialized as text.
//
// The view itself is here, i.e. the drawing and the keys and mouse buttons it handles. Where it is
// scrolled to is in ContentScroll and what is selected in ContentSelection, both of which are
// index math with no view, so they can be tested without a terminal.
class ContentView : View
{
    readonly GetContentCallback? onGetContent;
    readonly Dictionary<Key, OnKeyCallbackReturn> keys = new Dictionary<Key, OnKeyCallbackReturn>();
    readonly Dictionary<MouseFlags, OnMouseCallbackReturn> mouses = new Dictionary<MouseFlags, OnMouseCallbackReturn>();

    const int topBorderHeight = 1;
    const int cursorWidth = 1;
    const int verticalScrollbarWidth = 1;

    readonly IReadOnlyList<Text>? contentRows;
    readonly ContentScroll scroll;
    readonly ContentSelection selection = new ContentSelection();

    ContentView()
    {
        scroll = new ContentScroll(() => ViewHeight, () => ContentHeight);
        WantMousePositionReports = true;
        CanFocus = true;
    }

    internal ContentView(GetContentCallback onGetContent)
        : this()
    {
        this.onGetContent = onGetContent;
    }

    internal ContentView(IReadOnlyList<Text> content)
        : this()
    {
        this.contentRows = content;
        scroll.SetTotalCount(content.Count);
        SetNeedsDisplay();
    }

    public event Action? CurrentIndexChange
    {
        add => scroll.CurrentIndexChange += value;
        remove => scroll.CurrentIndexChange -= value;
    }

    public event Action<Selection>? SelectionChange;

    public bool IsFocus { get; set; } = true;
    public int FirstIndex => scroll.FirstIndex;
    public int TotalCount => scroll.TotalCount;
    public int CurrentIndex => scroll.CurrentIndex;

    public bool IsHighlightCurrentIndex { get; set; } = false;
    public int ViewHeight => Frame.Height;
    public int ViewWidth => Frame.Width;
    public bool IsShowCursor { get; set; } = true;
    public bool IsScrollMode { get; set; } = false;
    public bool IsCursorMargin { get; set; } = false;
    public bool IsTopBorder { get; set; } = false;
    public bool IsHideCursor { get; set; } = false;
    public int ContentX => IsCursorMargin ? cursorWidth : 0;
    public int ContentY => IsTopBorder ? topBorderHeight : 0;
    public int ContentWidth => Frame.Width - ContentX - verticalScrollbarWidth;
    public int ContentHeight => IsTopBorder ? ViewHeight - topBorderHeight : ViewHeight;
    public Point CurrentPoint => new Point(0, CurrentIndex - FirstIndex);
    public int SelectStartIndex => selection.StartIndex;
    public int SelectCount => selection.Count;
    public bool IsCustomShowSelection { get; set; } = false;

    public Selection Selection => selection.Selection;

    public void RegisterKeyHandler(Key key, OnKeyCallback callback)
    {
        keys[key] = () =>
        {
            callback();
            return true;
        };
    }

    public void RegisterKeyHandler(Key key, OnKeyCallbackReturn callback)
    {
        keys[key] = callback;
    }

    public void RegisterMouseHandler(MouseFlags mouseFlags, OnMouseCallback callback)
    {
        mouses[mouseFlags] = (x, y) =>
        {
            callback(x, y);
            return true;
        };
    }

    public void RegisterMouseHandler(MouseFlags mouseFlags, OnMouseCallbackReturn callback)
    {
        mouses[mouseFlags] = callback;
    }

    public void ScrollToShowIndex(int index, int margin = 5)
    {
        if (scroll.ScrollToShowIndex(index, margin))
            SetNeedsDisplay();
    }

    public override bool OnEnter(View view)
    {
        Application.Driver.SetCursorVisibility(CursorVisibility.Invisible);

        return base.OnEnter(view);
    }

    public override bool ProcessHotKey(KeyEvent keyEvent)
    {
        if (!HasFocus)
            return base.ProcessHotKey(keyEvent);

        // Log.Info($"HotKey: {keyEvent}, {keyEvent.Key}");

        if (keys.TryGetValue(keyEvent.Key, out var callback))
        {
            if (callback())
                return true;
        }

        switch (keyEvent.Key)
        {
            case Key.CursorUp:
                ClearSelection();
                Move(-1);
                return true;
            case Key.CursorUp | Key.ShiftMask:
                OnSelectUp();
                Move(-1);
                return true;
            case Key.PageUp:
                ClearSelection();
                Move(-(ContentHeight - 1));
                return true;
            case Key.CursorDown:
                ClearSelection();
                Move(1);
                return true;
            case Key.CursorDown | Key.ShiftMask:
                OnSelectDown();
                return true;
            case Key.PageDown:
                ClearSelection();
                Move(ContentHeight - 1);
                return true;
            case Key.Space:
                ClearSelection();
                Move(ContentHeight - 1);
                return true;
            case Key.Home:
                ClearSelection();
                Move(-TotalCount);
                return true;
            case Key.End:
                ClearSelection();
                Move(TotalCount);
                return true;
        }

        return base.ProcessHotKey(keyEvent);
    }

    public override bool MouseEvent(MouseEvent ev)
    {
        //Log.Info($"Mouse: {ev}, {ev.OfX}, {ev.OfY}, {ev.X}, {ev.Y}");
        if (!HasFocus)
            return base.MouseEvent(ev);

        if (mouses.TryGetValue(ev.Flags, out var callback))
        {
            if (callback(ev.X, ev.Y))
                return true;
        }

        if (ev.Flags.HasFlag(MouseFlags.WheeledDown))
        {
            Scroll(1);
            return true;
        }
        else if (ev.Flags.HasFlag(MouseFlags.WheeledUp))
        {
            Scroll(-1);
            return true;
        }

        if (ev.Flags.HasFlag(MouseFlags.Button1Pressed) && ev.Flags.HasFlag(MouseFlags.ReportMousePosition))
        {
            MouseDrag(ev);
            return true;
        }
        else if (ev.Flags.HasFlag(MouseFlags.Button1Pressed))
        {
            ClearSelection();
            return true;
        }

        if (ev.Flags.HasFlag(MouseFlags.ButtonShift) && ev.Flags.HasFlag(MouseFlags.ReportMousePosition))
        {
            MouseDrag(ev);
            return true;
        }

        return false;
    }

    public override void Redraw(Rect bounds)
    {
        Clear();

        IReadOnlyList<Text> currentRows = GetContentRows();

        int y = ContentY;
        currentRows.ForEach(
            (row, i) =>
            {
                Text txt = row;
                var index = i + FirstIndex;

                if (selection.IsSelected && !IsCustomShowSelection && HasFocus)
                {
                    var isRowSelected = selection.IsRowSelected(index);
                    if (isRowSelected && Selection.I1 == Selection.I2)
                    { // One row is selected, highlight the selected sub text
                        var x2 = Math.Min(Selection.X2, txt.Length);
                        var part1 = txt.Subtext(0, Selection.X1);
                        var part2 = txt.Subtext(Selection.X1, x2 - Selection.X1);
                        var part3 = txt.Subtext(x2, txt.Length - x2);
                        txt = part1.ToTextBuilder().Add(part2.ToSelect()).Add(part3);
                    }
                    else if (isRowSelected)
                    { // Multiple rows are selected, highlight the whole rows
                        txt = txt.ToSelect();
                    }
                }
                else
                {
                    txt = IsHighlightCurrentIndex && index == CurrentIndex && HasFocus ? row.ToHighlight() : row;
                }

                txt.Draw(this, ContentX, y++);
            }
        );

        DrawTopBorder();
        DrawCursor();
        DrawVerticalScrollbar();
    }

    public bool IsRowSelected(int index) => selection.IsRowSelected(index);

    public void ClearSelection()
    {
        if (selection.Clear())
            SetNeedsDisplay();
    }

    public string CopySelectedText()
    {
        if (!selection.IsSelected)
            return "";

        var copyText = new StringBuilder();

        var currentRows = GetContentRows();

        int y = ContentY;
        currentRows.ForEach(
            (row, i) =>
            {
                Text txt = row;
                var index = i + FirstIndex;

                if (!selection.IsRowSelected(index))
                    return;

                if (Selection.I1 == Selection.I2)
                { // One row is selected, copy selected sub text
                    var x2 = Math.Min(Selection.X2, txt.Length);

                    var part2 = txt.Subtext(Selection.X1, x2 - Selection.X1);
                    copyText.Append(part2.ToString());
                }
                else
                { // Multiple rows are selected, copy whole rows
                    copyText.AppendLine(txt.ToString());
                }

                txt.Draw(this, ContentX, y++);
            }
        );

        ClearSelection();
        SetNeedsDisplay();

        return copyText.ToString();
    }

    IReadOnlyList<Text> GetContentRows()
    {
        var drawCount = ContentHeight; //  Math.Min(ContentHeight, TotalCount - FirstIndex);

        if (contentRows != null)
        { // Use content provided in constructor
            return contentRows.Skip(FirstIndex).Take(drawCount).ToList();
        }

        var (rows, totalCount) = onGetContent!(FirstIndex, drawCount, CurrentIndex, ContentWidth);
        IReadOnlyList<Text> currentRows = rows.ToList();
        scroll.SetTotalCount(totalCount);

        while (!currentRows.Any() && TotalCount > 0)
        { // TotalCount now less than previous FirstIndex, need to adjust FirstIndex and CurrentIndex and try again
            scroll.MoveToEndOfContent();
            (rows, totalCount) = onGetContent!(FirstIndex, drawCount, CurrentIndex, ContentWidth);
            currentRows = rows.ToList();
            scroll.SetTotalCount(totalCount);
        }

        return currentRows;
    }

    void OnSelectUp()
    {
        if (selection.SelectUp(CurrentIndex))
            Move(-1);
        SetNeedsDisplay();
    }

    void OnSelectDown()
    {
        if (selection.SelectDown(CurrentIndex, TotalCount))
            Move(1);
        SetNeedsDisplay();
    }

    void MouseDrag(MouseEvent ev)
    {
        var i = ev.Y + FirstIndex + (IsTopBorder ? -1 : 0);

        var direction = selection.Drag(ev.X, i);
        if (direction < 0 && ev.Y <= 2)
        { // Dragging up at the top of the view, scroll to show the rows above
            Scroll(-1);
        }
        else if (direction > 0 && ev.Y >= ContentHeight - 2)
        { // Dragging down at the bottom of the view, scroll to show the rows below
            Scroll(1);
        }

        SetNeedsDisplay();
        SelectionChange?.Invoke(Selection);
    }

    void DrawTopBorder()
    {
        if (!IsTopBorder)
        {
            return;
        }
        Move(0, 0);
        if (IsFocus)
        {
            Driver.SetAttribute(Color.White);
            Driver.AddStr(new string('━', ViewWidth));
        }
        else
        {
            Driver.SetAttribute(Color.Dark);
            Driver.AddStr(new string('─', ViewWidth));
        }
    }

    void DrawCursor()
    {
        if (!IsShowCursor || IsHideCursor || !IsFocus || !HasFocus)
        {
            return;
        }

        Move(0, ContentY + (CurrentIndex - FirstIndex));
        Driver.SetAttribute(Color.White);
        Driver.AddStr("┃");
    }

    void DrawVerticalScrollbar()
    {
        (int sbStart, int sbEnd) = scroll.GetVerticalScrollbarIndexes();

        var color = HasFocus ? Color.Magenta : Color.Dark;
        var x = Math.Max(ViewWidth - 1, 0);
        for (int i = sbStart; i <= sbEnd; i++)
        {
            Move(x, i + ContentY);
            Driver.SetAttribute(color);
            Driver.AddStr("┃");
        }
    }

    internal void Scroll(int count)
    {
        if (scroll.Scroll(count))
            SetNeedsDisplay();
    }

    internal void MoveToTop() => Move(-FirstIndex);

    internal void SetIndexAtViewY(int viewY)
    {
        int currentViewY = CurrentIndex - FirstIndex;

        Move(viewY - currentViewY);
    }

    internal void SetCurrentIndex(int index)
    {
        scroll.SetCurrentIndex(index);
        SetNeedsDisplay();
    }

    internal void Move(int move)
    {
        if (IsScrollMode)
        {
            Scroll(move);
            return;
        }

        if (scroll.Move(move))
            SetNeedsDisplay();
    }
}
