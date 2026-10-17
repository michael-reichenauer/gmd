using System.Drawing;
using System.Text;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Color = gmd.Cui.Common.Color;

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
    readonly Dictionary<Key, OnKeyCallbackReturn> keys = [];
    readonly Dictionary<MouseFlags, OnMouseCallbackReturn> mouses = [];

    const int topBorderHeight = 1;
    const int verticalScrollbarWidth = 1;

    readonly IReadOnlyList<Text>? contentRows;
    readonly ContentScroll scroll;
    readonly ContentSelection selection = new ContentSelection();

    ContentView()
    {
        scroll = new ContentScroll(() => ContentHeight);
        MousePositionTracking = true;
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
        SetNeedsDraw();
    }

    public event Action? CurrentIndexChange
    {
        add => scroll.CurrentIndexChange += value;
        remove => scroll.CurrentIndexChange -= value;
    }

    // Whether the view has the focus as drawn, where two views share the keys and Tab moves between
    // them: the log and its commit details, and the blame and its. A view on its own leaves it on.
    public bool IsFocus { get; set; } = true;

    // Set on a view that is sent its keys by another, which keeps Terminal.Gui's focus and forwards
    // them, as the log does to its commit details once Tab moves into them. Tab does not move
    // Terminal.Gui's focus, so IsFocus alone says whether such a view is focused.
    public bool IsKeysForwarded { get; set; } = false;

    public int FirstIndex => scroll.FirstIndex;
    public int TotalCount => scroll.TotalCount;
    public int CurrentIndex => scroll.CurrentIndex;

    public bool IsHighlightCurrentIndex { get; set; } = false;
    public int ViewHeight => Viewport.Height;
    public int ViewWidth => Viewport.Width;
    public bool IsShowCursor { get; set; } = true;
    public bool IsScrollMode { get; set; } = false;
    public bool IsTopBorder { get; set; } = false;

    // A row drawn above the rows in place of the top border, from the index of the first row shown
    // and the width, e.g. the diff's file and hunk the top of the view is in. Drawn in the same draw
    // as the rows, so it follows a scroll at once, which a view of its own would only on the next key.
    public Func<int, int, Text>? TopRow { get; set; }

    public bool IsHideCursor { get; set; } = false;
    public int ContentY => HasTopRow ? topBorderHeight : 0;
    public int ContentWidth => Viewport.Width - verticalScrollbarWidth;
    public int ContentHeight => HasTopRow ? ViewHeight - topBorderHeight : ViewHeight;
    bool HasTopRow => IsTopBorder || TopRow != null;
    public Point CurrentPoint => new Point(0, CurrentIndex - FirstIndex);
    public int SelectStartIndex => selection.StartIndex;
    public int SelectCount => selection.Count;
    public bool IsCustomShowSelection { get; set; } = false;

    public Selection Selection => selection.Selection;

    // Drawn as the view the keys act on: the top border highlighted, the cursor, and the scrollbar in magenta
    bool IsFocused => IsFocus && (HasFocus || IsKeysForwarded);

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

    // A letter in both cases, given in lower case. Keys are looked up by exact value, so without this
    // 'M' is not 'm' — and a menu writes its shortcuts in upper case, so that is what gets pressed.
    // The log view registers its letters one case at a time on purpose, since 'p' and 'P' are
    // different commands there.
    public void RegisterLetterHandler(Key letter, OnKeyCallback callback)
    {
        Asserter.Requires(letter.IsKeyCodeAtoZ && !letter.IsShift);
        RegisterKeyHandler(letter, callback);
        RegisterKeyHandler(letter.WithShift, callback);
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
            SetNeedsDraw();
    }

    // SPIKE: 2.x calls OnKeyDown on the focused view first, then its superviews', so this is what 1.x's
    // ProcessHotKey was here. A Key is a class with a KeyCode, compared by it, and Shift a property of
    // it, so the switch on key values became a chain of comparisons.
    protected override bool OnKeyDown(Key key)
    {
        // Log.Info($"Key: {key}");

        if (keys.TryGetValue(key, out var callback))
        {
            if (callback())
                return true;
        }

        if (key == Key.CursorUp)
        {
            ClearSelection();
            Move(-1);
            return true;
        }
        if (key == Key.CursorUp.WithShift)
        {
            OnSelectUp();
            return true;
        }
        if (key == Key.PageUp)
        {
            ClearSelection();
            Move(-(ContentHeight - 1));
            return true;
        }
        if (key == Key.CursorDown)
        {
            ClearSelection();
            Move(1);
            return true;
        }
        if (key == Key.CursorDown.WithShift)
        {
            OnSelectDown();
            return true;
        }
        if (key == Key.PageDown || key == Key.Space)
        {
            ClearSelection();
            Move(ContentHeight - 1);
            return true;
        }
        if (key == Key.Home)
        {
            ClearSelection();
            Move(-TotalCount);
            return true;
        }
        if (key == Key.End)
        {
            ClearSelection();
            Move(TotalCount);
            return true;
        }

        return base.OnKeyDown(key);
    }

    // SPIKE: the mouse is the Mouse of 2.x, its Position relative to the viewport, which here is the frame
    protected override bool OnMouseEvent(Mouse ev)
    {
        var (evX, evY) = ev.Position is Point p ? (p.X, p.Y) : (0, 0);
        //Log.Info($"Mouse: {ev}, {ev.OfX}, {ev.OfY}, {ev.X}, {ev.Y}");
        // A view that never takes the focus, the resolver's result pane, is scrolled by the wheel
        // over it, the one way the mouse has to it
        if (!HasFocus && !CanFocus && ev.Flags.HasFlag(MouseFlags.WheeledDown))
        {
            Scroll(1);
            return true;
        }
        if (!HasFocus && !CanFocus && ev.Flags.HasFlag(MouseFlags.WheeledUp))
        {
            Scroll(-1);
            return true;
        }
        // and a click on it goes to what it registered for it, e.g. the diff's list of files, which
        // goes to the file clicked on
        if (!HasFocus && !CanFocus && mouses.TryGetValue(ev.Flags, out var unfocusedCallback))
        {
            unfocusedCallback(evX, evY);
            return true;
        }

        if (!HasFocus)
            return base.OnMouseEvent(ev);

        if (mouses.TryGetValue(ev.Flags, out var callback))
        {
            if (callback(evX, evY))
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

        if (ev.Flags.HasFlag(MouseFlags.LeftButtonPressed) && ev.Flags.HasFlag(MouseFlags.PositionReport))
        {
            MouseDrag(evX, evY);
            return true;
        }
        else if (ev.Flags.HasFlag(MouseFlags.LeftButtonPressed))
        {
            ClearSelection();
            selection.Press(evX, IndexAtY(evY));
            return true;
        }

        if (ev.Flags.HasFlag(MouseFlags.Shift) && ev.Flags.HasFlag(MouseFlags.PositionReport))
        {
            MouseDrag(evX, evY);
            return true;
        }

        return false;
    }

    // SPIKE: 2.x clears the viewport with the view's scheme before this, which the rows then cover; the
    // background left below the last row is the scheme's, so the top view's scheme is gmd's colors
    protected override bool OnDrawingContent(DrawContext? context)
    {
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

                txt.Draw(this, 0, y++);
            }
        );

        DrawTopBorder();
        DrawCursor();
        DrawVerticalScrollbar();
        return true;
    }

    public bool IsRowSelected(int index) => selection.IsRowSelected(index);

    public void ClearSelection()
    {
        if (selection.Clear())
            SetNeedsDraw();
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

                txt.Draw(this, 0, y++);
            }
        );

        ClearSelection();
        SetNeedsDraw();

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
        SetNeedsDraw();
    }

    void OnSelectDown()
    {
        if (selection.SelectDown(CurrentIndex, TotalCount))
            Move(1);
        SetNeedsDraw();
    }

    // The row index of a view row, below the top border if there is one
    int IndexAtY(int y) => y + FirstIndex - ContentY;

    void MouseDrag(int x, int y)
    {
        var direction = selection.Drag(x, IndexAtY(y), TotalCount);
        if (direction < 0 && y <= 2)
        { // Dragging up at the top of the view, scroll to show the rows above
            Scroll(-1);
        }
        else if (direction > 0 && y >= ContentHeight - 2)
        { // Dragging down at the bottom of the view, scroll to show the rows below
            Scroll(1);
        }

        SetNeedsDraw();
    }

    void DrawTopBorder()
    {
        if (TopRow != null)
        {
            TopRow(FirstIndex, ViewWidth).Subtext(0, ViewWidth, isFillRest: true).Draw(this, 0, 0);
            return;
        }
        if (!IsTopBorder)
        {
            return;
        }
        Move(0, 0);
        if (IsFocused)
        {
            SetAttribute(Color.White);
            AddStr(new string('━', ViewWidth));
        }
        else
        {
            SetAttribute(Color.Dark);
            AddStr(new string('─', ViewWidth));
        }
    }

    void DrawCursor()
    {
        if (!IsShowCursor || IsHideCursor || !IsFocused)
        {
            return;
        }

        Move(0, ContentY + (CurrentIndex - FirstIndex));
        SetAttribute(Color.White);
        AddStr("┃");
    }

    void DrawVerticalScrollbar()
    {
        (int sbStart, int sbEnd) = scroll.GetVerticalScrollbarIndexes();

        var color = IsFocused ? Color.Magenta : Color.Dark;
        var x = Math.Max(ViewWidth - 1, 0);
        for (int i = sbStart; i <= sbEnd; i++)
        {
            Move(x, i + ContentY);
            SetAttribute(color);
            AddStr("┃");
        }
    }

    internal void Scroll(int count)
    {
        if (scroll.Scroll(count))
            SetNeedsDraw();
    }

    // Moves the cursor to the first row, which takes the first shown row with it, so that the view
    // shows the top of the content wherever in the view the cursor was.
    internal void MoveToTop() => Move(-CurrentIndex);

    internal void SetIndexAtViewY(int viewY)
    {
        int currentViewY = CurrentIndex - FirstIndex;

        Move(viewY - ContentY - currentViewY);
    }

    // Tells a view whose rows come from its callback how many there are, once they are made anew,
    // e.g. the diff laid out in one column. The callback says so only as the view draws, so a row put
    // the cursor on before then, past the count of the rows before, was scrolled to only as far as
    // that count went, and the cursor was left out of sight.
    internal void SetTotalCount(int totalCount) => scroll.SetTotalCount(totalCount);

    // Puts the cursor on a row and scrolls it into view. The scroll comes first: scrolling moves the
    // cursor with the rows, so a cursor set before it ended up as far off as the view scrolled.
    internal void ShowCurrentIndex(int index)
    {
        ScrollToShowIndex(index);
        SetCurrentIndex(index);
    }

    internal void SetCurrentIndex(int index)
    {
        scroll.SetCurrentIndex(index);
        SetNeedsDraw();
    }

    internal void Move(int move)
    {
        if (IsScrollMode)
        {
            Scroll(move);
            return;
        }

        if (scroll.Move(move))
            SetNeedsDraw();
    }
}
