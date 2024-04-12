using System.Text;
using Terminal.Gui;

namespace gmd.Cui.Common;


internal delegate (IEnumerable<Text> rows, int total) GetContentCallback(int firstIndex, int count, int currentIndex, int contentWidth);
internal delegate void OnKeyCallback();
internal delegate bool OnKeyCallbackReturn();
internal delegate void OnMouseCallback(int x, int y);
internal delegate bool OnMouseCallbackReturn(int x, int y);
record class Selection(int X1, int I1, int X2, int I2, int InitialIndex)
{
    public bool IsEmpty => X1 == X2 && I1 == I2;
}


class ContentView : View
{
    readonly GetContentCallback? onGetContent;
    readonly Dictionary<Key, OnKeyCallbackReturn> keys = new Dictionary<Key, OnKeyCallbackReturn>();
    readonly Dictionary<MouseFlags, OnMouseCallbackReturn> mouses = new Dictionary<MouseFlags, OnMouseCallbackReturn>();

    const int topBorderHeight = 1;
    const int cursorWidth = 1;
    const int verticalScrollbarWidth = 1;

    readonly bool isMoveUpDownWrap = false;  // Not used yet
    readonly IReadOnlyList<Text>? contentRows;

    int currentIndex = 0;
    bool isSelected = false;

    Selection selection = new Selection(0, 0, 0, 0, 0);
    Point lastMousePoint = new Point(0, 0);


    internal ContentView(GetContentCallback onGetContent)
    {
        this.onGetContent = onGetContent;
        WantMousePositionReports = true;
        CanFocus = true;
    }

    internal ContentView(IReadOnlyList<Text> content)
    {
        this.contentRows = content;
        TotalCount = content.Count;
        WantMousePositionReports = true;
        CanFocus = true;
        SetNeedsDisplay();
    }

    public event Action? CurrentIndexChange;
    public event Action<Selection>? SelectionChange;


    public bool IsFocus { get; set; } = true;
    public int FirstIndex { get; private set; } = 0;
    public int TotalCount { get; private set; } = 0;

    public int CurrentIndex
    {
        get { return currentIndex; }
        private set
        {
            var v = currentIndex;
            currentIndex = value;
            if (v != value) CurrentIndexChange?.Invoke();
        }
    }



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
    public int SelectStartIndex => selection.I1;
    public int SelectCount => isSelected ? selection.I2 - selection.I1 + 1 : 0;
    public bool IsCustomShowSelection { get; set; } = false;

    public Selection Selection => selection;

    public void RegisterKeyHandler(Key key, OnKeyCallback callback)
    {
        keys[key] = () => { callback(); return true; };
    }
    public void RegisterKeyHandler(Key key, OnKeyCallbackReturn callback)
    {
        keys[key] = callback;
    }

    public void RegisterMouseHandler(MouseFlags mouseFlags, OnMouseCallback callback)
    {
        mouses[mouseFlags] = (x, y) => { callback(x, y); return true; };
    }
    public void RegisterMouseHandler(MouseFlags mouseFlags, OnMouseCallbackReturn callback)
    {
        mouses[mouseFlags] = callback;
    }

    public void ScrollToShowIndex(int index, int margin = 5)
    {
        if (index >= FirstIndex + margin && index <= FirstIndex + ContentHeight - margin)
        {
            // index already shown
            return;
        }

        int scroll = index - FirstIndex - 5;
        Scroll(scroll);
    }

    public override bool OnEnter(View view)
    {
        Application.Driver.SetCursorVisibility(CursorVisibility.Invisible);

        return base.OnEnter(view);
    }

    public override bool ProcessHotKey(KeyEvent keyEvent)
    {
        if (!HasFocus) return base.ProcessHotKey(keyEvent);

        // Log.Info($"HotKey: {keyEvent}, {keyEvent.Key}");

        if (keys.TryGetValue(keyEvent.Key, out var callback))
        {
            if (callback()) return true;
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
        if (!HasFocus) return base.MouseEvent(ev);

        if (mouses.TryGetValue(ev.Flags, out var callback))
        {
            if (callback(ev.X, ev.Y)) return true;
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
            MouseDrag(ev, false);
            return true;
        }
        else if (ev.Flags.HasFlag(MouseFlags.Button1Pressed))
        {
            ClearSelection();
            return true;
        }

        if (ev.Flags.HasFlag(MouseFlags.ButtonShift) && ev.Flags.HasFlag(MouseFlags.ReportMousePosition))
        {
            MouseDrag(ev, true);
            return true;
        }

        return false;
    }

    public override void Redraw(Rect bounds)
    {
        Clear();

        IReadOnlyList<Text> currentRows = GetContentRows();

        int y = ContentY;
        currentRows.ForEach((row, i) =>
        {
            Text txt = row;
            var index = i + FirstIndex;

            if (isSelected && !IsCustomShowSelection && HasFocus)
            {
                var isRowSelected = index >= selection.I1 && index <= selection.I2;
                if (isRowSelected && selection.I1 == selection.I2)
                {   // One row is selected, highlight the selected sub text
                    var x2 = Math.Min(selection.X2, txt.Length);
                    var part1 = txt.Subtext(0, selection.X1);
                    var part2 = txt.Subtext(selection.X1, x2 - selection.X1);
                    var part3 = txt.Subtext(x2, txt.Length - x2);
                    txt = part1.ToTextBuilder().Add(part2.ToSelect()).Add(part3);
                }
                else if (isRowSelected)
                {   // Multiple rows are selected, highlight the whole rows
                    txt = txt.ToSelect();
                }
            }
            else
            {
                txt = IsHighlightCurrentIndex && index == currentIndex && HasFocus ? row.ToHighlight() : row;
            }

            txt.Draw(this, ContentX, y++);
        });

        DrawTopBorder();
        DrawCursor();
        DrawVerticalScrollbar();
    }



    public bool IsRowSelected(int index) => isSelected && index >= selection.I1 && index <= selection.I2;

    public void ClearSelection()
    {
        if (!isSelected) return;
        isSelected = false;
        selection = new Selection(0, 0, 0, 0, 0);
        SetNeedsDisplay();
    }

    public string CopySelectedText()
    {
        if (!isSelected) return "";

        var copyText = new StringBuilder();
        var drawCount = Math.Min(ContentHeight, TotalCount - FirstIndex);

        var currentRows = GetContentRows();

        int y = ContentY;
        currentRows.ForEach((row, i) =>
        {
            Text txt = row;
            var index = i + FirstIndex;

            var isRowSelected = index >= selection.I1 && index <= selection.I2;
            if (!isRowSelected) return;

            if (selection.I1 == selection.I2)
            {   // One row is selected, copy selected sub text
                var x2 = Math.Min(selection.X2, txt.Length);

                var part2 = txt.Subtext(selection.X1, x2 - selection.X1);
                copyText.Append(part2.ToString());
            }
            else
            {   // Multiple rows are selected, copy whole rows
                copyText.AppendLine(txt.ToString());
            }

            txt.Draw(this, ContentX, y++);
        });

        ClearSelection();
        SetNeedsDisplay();

        return copyText.ToString();
    }

    IReadOnlyList<Text> GetContentRows()
    {
        var drawCount = ContentHeight; //  Math.Min(ContentHeight, TotalCount - FirstIndex);


        if (contentRows != null)
        {   // Use content provided in constructor
            return contentRows.Skip(FirstIndex).Take(drawCount).ToList();
        }

        var (rows, totalCount) = onGetContent!(FirstIndex, drawCount, CurrentIndex, ContentWidth);
        IReadOnlyList<Text> currentRows = rows.ToList();
        TotalCount = totalCount;

        while (!currentRows.Any() && TotalCount > 0)
        {   // TotalCount now less than previous FirstIndex, need to adjust FirstIndex and CurrentIndex and try again
            FirstIndex = Math.Max(0, TotalCount - 3);
            CurrentIndex = TotalCount - 1;
            (rows, totalCount) = onGetContent!(FirstIndex, drawCount, CurrentIndex, ContentWidth);
            currentRows = rows.ToList();
            TotalCount = totalCount;
        }

        return currentRows;
    }


    void OnSelectUp()
    {
        int currentIndex = CurrentIndex;

        if (!isSelected)
        {   // Start selection of current row
            isSelected = true;
            selection = new Selection(0, currentIndex, int.MaxValue, currentIndex, currentIndex);
            SetNeedsDisplay();
            return;
        }

        if (currentIndex == 0)
        {   // Already at top of page, no need to move         
            return;
        }

        if (currentIndex <= selection.I1)
        {   // Expand selection upp
            selection = selection with { I1 = currentIndex - 1 };
        }
        else
        {   // Shrink selection upp
            selection = selection with { I2 = currentIndex - 1 };
        }

        Move(-1);
    }


    void OnSelectDown()
    {
        int currentIndex = CurrentIndex;

        if (!isSelected)
        {   // Start selection of current row
            isSelected = true;
            selection = new Selection(0, currentIndex, int.MaxValue, currentIndex, currentIndex);
            SetNeedsDisplay();
            return;
        }

        if (currentIndex >= TotalCount - 1)
        {   // Already at bottom of page, no need to move         
            return;
        }

        if (currentIndex >= selection.I2)
        {   // Expand selection down
            selection = selection with { I2 = currentIndex + 1 };
        }
        else if (currentIndex <= selection.I1)
        {   // Shrink selection down
            selection = selection with { I1 = currentIndex + 1 };
        }

        Move(1);
    }

    void MouseDrag(MouseEvent ev, bool _)
    {
        var x = ev.X;
        var i = ev.Y + FirstIndex + (IsTopBorder ? -1 : 0);

        if (!isSelected)
        {   // Start mouse dragging
            isSelected = true;
            selection = new Selection(x, i, x, i, i);
            lastMousePoint = new Point(x, i);
        }

        var x1 = selection.X1;
        var i1 = selection.I1;
        var x2 = selection.X2;
        var i2 = selection.I2;

        if (x < lastMousePoint.X)
        {   // Moving left, expand selection on left or shrink selection on right side
            if (x < x1) x1 = x; else x2 = x;
        }
        else if (x > lastMousePoint.X)
        {   // Moving right expand selection on right or shrink selection on left side
            if (x > x2) x2 = x; else x1 = x;
        }

        if (i < lastMousePoint.Y)
        {   // Moving upp, expand selection upp or shrink selection on bottom side
            if (ev.Y <= 2) Scroll(-1);
            if (i < i1) i1 = i; else i2 = i;
        }
        else if (i > lastMousePoint.Y)
        {   // // Moving down, expand selection down or shrink selection on top side
            if (ev.Y >= ContentHeight - 2) Scroll(1);
            if (i > i2) i2 = i; else i1 = i;
        }

        selection = new Selection(x1, i1, x2, i2, selection.InitialIndex);
        lastMousePoint = new Point(x, i);
        // Log.Info($"Mouse Drag: {mouseDrag}");

        SetNeedsDisplay();
        SelectionChange?.Invoke(selection);
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


    internal void Scroll(int scroll)
    {
        if (TotalCount == 0)
        {   // Cannot scroll empty view
            return;
        }

        int newFirst = FirstIndex + scroll;

        if (newFirst < 0) newFirst = 0;

        if (newFirst + ViewHeight >= TotalCount)
        {
            newFirst = TotalCount - ViewHeight;
        }
        if (newFirst < 0) newFirst = 0;
        if (newFirst == FirstIndex)
        {   // No move, reached top or bottom
            return;
        }

        int newCurrent = CurrentIndex + (newFirst - FirstIndex);

        if (newCurrent < newFirst)
        {   // Need to scroll view up to the new current line
            newCurrent = newFirst;
        }
        if (newCurrent >= newFirst + ContentHeight)
        {   // Need to scroll view down to the new current line
            newCurrent = newFirst - ContentHeight - 1;
        }
        if (newCurrent < 0) newCurrent = 0;

        FirstIndex = newFirst;
        CurrentIndex = newCurrent;

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
        CurrentIndex = index;
        SetNeedsDisplay();
    }


    internal void Move(int move)
    {
        if (IsScrollMode)
        {
            Scroll(move);
            return;
        }

        // Log.Info($"move {move}, current: {currentIndex}");
        if (TotalCount == 0)
        {   // Cannot scroll empty view
            return;
        }

        int newCurrent = CurrentIndex + move;

        if (newCurrent < 0)
        {   // Reached top, wrap or stay
            newCurrent = isMoveUpDownWrap ? TotalCount - 1 : 0;
        }

        if (newCurrent >= TotalCount)
        {   // Reached bottom, wrap or stay 
            newCurrent = isMoveUpDownWrap ? 0 : TotalCount - 1;
        }

        if (newCurrent == CurrentIndex)
        {   // No move, reached top or bottom
            return;
        }

        CurrentIndex = newCurrent;

        if (CurrentIndex < FirstIndex)
        {   // Need to scroll view up to the new current line 
            FirstIndex = CurrentIndex;
        }

        if (CurrentIndex >= FirstIndex + ViewHeight)
        {  // Need to scroll view down to the new current line
            FirstIndex = CurrentIndex - ViewHeight + 1;
        }
        // Log.Info($"move {move}, current: {currentIndex}, first: {FirstIndex}");

        SetNeedsDisplay();
    }

    void DrawVerticalScrollbar()
    {
        (int sbStart, int sbEnd) = GetVerticalScrollbarIndexes();

        var color = HasFocus ? Color.Magenta : Color.Dark;
        var x = Math.Max(ViewWidth - 1, 0);
        for (int i = sbStart; i <= sbEnd; i++)
        {
            Move(x, i + ContentY);
            Driver.SetAttribute(color);
            Driver.AddStr("┃");
        }
    }

    (int, int) GetVerticalScrollbarIndexes()
    {
        if (TotalCount == 0 || TotalCount <= ContentHeight)
        {   // No need for a scrollbar
            return (0, -1);
        }

        float scrollbarFactor = (float)ContentHeight / (float)TotalCount;

        int sbStart = (int)Math.Floor((float)FirstIndex * scrollbarFactor);
        int sbSize = (int)Math.Ceiling((float)ContentHeight * scrollbarFactor);

        if (sbStart + sbSize + 1 > ContentHeight)
        {
            sbStart = Frame.Height - sbSize - 1;
            if (sbStart < 0)
            {
                sbStart = 0;
            }
        }

        return (sbStart, sbStart + sbSize);
    }
}
