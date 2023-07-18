using Terminal.Gui;

namespace gmd.Cui.Common;


internal delegate IEnumerable<Text> GetContentCallback(int firstIndex, int count, int currentIndex, int contentWidth);
internal delegate void OnKeyCallback();
internal delegate void OnMouseCallback(int x, int y);
record class MouseDrag(int X1, int Y1, int X2, int Y2);


class ContentView : View
{
    readonly GetContentCallback? onGetContent;
    readonly Dictionary<Key, OnKeyCallback> keys = new Dictionary<Key, OnKeyCallback>();
    readonly Dictionary<MouseFlags, OnMouseCallback> mouses = new Dictionary<MouseFlags, OnMouseCallback>();

    const int topBorderHeight = 1;
    const int cursorWidth = 1;
    const int verticalScrollbarWidth = 1;
    const int contentXMargin = cursorWidth + verticalScrollbarWidth;
    readonly bool isMoveUpDownWrap = false;  // Not used yet
    readonly IReadOnlyList<Text>? content;

    List<Text> currentRows = new List<Text>();
    int currentIndex = 0;
    int selectStartIndex = -1;
    int selectEndIndex = -1;
    bool isSelecting = false;
    bool isSelected = false;
    MouseDrag mouseDrag = new MouseDrag(0, 0, 0, 0);
    Point point = new Point(0, 0);


    internal ContentView(GetContentCallback onGetContent)
    {
        this.onGetContent = onGetContent;
    }

    internal ContentView(IReadOnlyList<Text> content)
    {
        this.content = content;
        this.TriggerUpdateContent(this.content.Count);
    }

    public event Action? CurrentIndexChange;
    public event Action<MouseDrag>? MouseDragged;
    public event Action<MouseDrag>? MouseDragging;


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
    public int SelectStartIndex => selectStartIndex;
    public int SelectCount => selectStartIndex == -1 ? 0 : selectEndIndex - selectStartIndex + 1;


    public void RegisterKeyHandler(Key key, OnKeyCallback callback)
    {
        keys[key] = callback;
    }

    public void RegisterMouseHandler(MouseFlags mouseFlags, OnMouseCallback callback)
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


    public void TriggerUpdateContent(int totalCount)
    {
        this.TotalCount = totalCount;
        if (FirstIndex > totalCount)
        {
            FirstIndex = totalCount - 1;
        }
        if (CurrentIndex < FirstIndex)
        {
            CurrentIndex = FirstIndex;
        }
        if (CurrentIndex > FirstIndex + ContentHeight)
        {
            CurrentIndex = FirstIndex + ContentHeight - 1;
        }
        CurrentIndex = Math.Min(totalCount - 1, CurrentIndex);
        CurrentIndex = Math.Max(0, CurrentIndex);

        SetNeedsDisplay();
    }

    public override bool ProcessHotKey(KeyEvent keyEvent)
    {
        if (!IsFocus) return false;

        if (keys.TryGetValue(keyEvent.Key, out var callback))
        {
            callback();
            return true;
        }

        switch (keyEvent.Key)
        {
            case Key.i:
                ToggleShowCursor();
                return true;
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
                Move(-Math.Max(0, ContentHeight - 1));
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
                Move(Math.Max(0, ContentHeight - 1));
                return true;
            case Key.Home:
                ClearSelection();
                Move(-Math.Max(0, TotalCount));
                return true;
            case Key.End:
                ClearSelection();
                Move(Math.Max(0, TotalCount));
                return true;
        }

        return true;
    }

    public override bool MouseEvent(MouseEvent ev)
    {
        //Log.Info($"Mouse: {ev}, {ev.OfX}, {ev.OfY}, {ev.X}, {ev.Y}");

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

        if (mouses.TryGetValue(ev.Flags, out var callback))
        {
            callback(ev.X, ev.Y);
            return true;
        }

        if (ev.Flags.HasFlag(MouseFlags.Button1Pressed) && ev.Flags.HasFlag(MouseFlags.ReportMousePosition))
        {
            MouseDrag(ev);
            return true;
        }

        if (ev.Flags.HasFlag(MouseFlags.Button1Released))
        {
            MouseReleased(ev);
            return true;
        }

        return false;
    }

    public override void Redraw(Rect bounds)
    {
        Clear();

        if (isSelecting)
        {
            RedrawSelect();
        }
        else
        {
            RedrawNormal();
        }

        DrawTopBorder();
        DrawCursor();
        DrawVerticalScrollbar();
    }


    public void RedrawNormal()
    {
        var drawCount = Math.Min(ContentHeight, TotalCount - FirstIndex);

        currentRows = (content != null)
            ? content.Skip(FirstIndex).Take(drawCount).ToList()
            : onGetContent!(FirstIndex, drawCount, CurrentIndex, ContentWidth).ToList();

        int y = ContentY;
        currentRows.ForEach((row, i) =>
        {
            var index = i + FirstIndex;
            Text txt = IsHighlightCurrentIndex && index == currentIndex ? row.ToHighlight() : row;
            txt.Draw(this, ContentX, y++);
        });
    }

    public void RedrawSelect()
    {
        var drawCount = currentRows.Count;

        int y = ContentY;
        currentRows.ForEach((row, i) =>
        {
            Text txt = row;
            var index = i + FirstIndex;
            var isRowSelected = index >= mouseDrag.Y1 && index <= mouseDrag.Y2;
            if (isRowSelected && mouseDrag.Y1 == mouseDrag.Y2)
            {   // One row is selected, highlight the selected sub text
                var part1 = txt.Subtext(0, mouseDrag.X1);
                var part2 = txt.Subtext(mouseDrag.X1, mouseDrag.X2 - mouseDrag.X1);
                var part3 = txt.Subtext(mouseDrag.X2, txt.Length - mouseDrag.X2);
                txt = part1.ToTextBuilder().Add(part2.ToSelect()).Add(part3);
            }
            else if (isRowSelected)
            {   // Multiple rows are selected, highlight the whole rows
                txt = txt.ToSelect();
            }

            txt.Draw(this, ContentX, y++);
        });
    }


    public bool IsRowSelected(int index) => selectStartIndex != -1 && index >= selectStartIndex && index <= selectEndIndex;

    public void ClearSelection()
    {
        if (selectStartIndex == -1) return;

        selectStartIndex = -1;
        selectEndIndex = -1;
        SetNeedsDisplay();
    }


    // Toggle showing/hiding cursor. Cursor is needed to select text for copy
    public void ToggleShowCursor()
    {
        ClearSelection();
        IsShowCursor = !IsShowCursor;
        IsScrollMode = !IsShowCursor;
        SetNeedsDisplay();
    }


    void OnSelectUp()
    {
        int currentIndex = CurrentIndex;

        if (selectStartIndex == -1)
        {   // Start selection of current row
            selectStartIndex = currentIndex;
            selectEndIndex = currentIndex;
            SetNeedsDisplay();
            return;
        }

        if (currentIndex == 0)
        {   // Already at top of page, no need to move         
            return;
        }

        if (currentIndex <= selectStartIndex)
        {   // Expand selection upp
            selectStartIndex = currentIndex - 1;
        }
        else
        {   // Shrink selection upp
            selectEndIndex = selectEndIndex - 1;
        }

        Move(-1);
    }


    void OnSelectDown()
    {
        int currentIndex = CurrentIndex;

        if (selectStartIndex == -1)
        {   // Start selection of current row
            selectStartIndex = currentIndex;
            selectEndIndex = currentIndex;
            SetNeedsDisplay();
            return;
        }

        if (currentIndex >= TotalCount - 1)
        {   // Already at bottom of page, no need to move         
            return;
        }

        if (currentIndex >= selectEndIndex)
        {   // Expand selection down
            selectEndIndex = currentIndex + 1;
        }
        else if (currentIndex <= selectStartIndex)
        {   // Shrink selection down
            selectStartIndex = selectStartIndex + 1;
        }

        Move(1);
    }

    void MouseDrag(MouseEvent ev)
    {
        isSelected = false;
        var x = ev.X;
        var y = ev.Y + FirstIndex + (IsTopBorder ? -1 : 0);

        if (!isSelecting)
        {   // Start mouse dragging
            isSelecting = true;
            mouseDrag = new MouseDrag(x, y, x, y);
            point = new Point(x, y);
            Log.Info($"Mouse Start Drag: {mouseDrag}");
        }

        var x1 = mouseDrag.X1;
        var y1 = mouseDrag.Y1;
        var x2 = mouseDrag.X2;
        var y2 = mouseDrag.Y2;

        if (x < point.X)
        {   // Moving left, expand selection on left or shrink selection on right side
            if (x < x1) x1 = x; else x2 = x;
        }
        else if (x > point.X)
        {   // Moving right expand selection on right or shrink selection on left side
            if (x > x2) x2 = x; else x1 = x;
        }

        if (y < point.Y)
        {   // Moving upp, expand selection upp or shrink selection on bottom side
            if (y < y1) y1 = y; else y2 = y;
        }
        else if (y > point.Y)
        {   // // Moving down, expand selection down or shrink selection on top side
            if (y > y2) y2 = y; else y1 = y;
        }

        mouseDrag = new MouseDrag(x1, y1, x2, y2);
        point = new Point(x, y);
        // Log.Info($"Mouse Drag: {mouseDrag}");

        SetNeedsDisplay();
        MouseDragging?.Invoke(mouseDrag);
    }

    void MouseReleased(MouseEvent ev)
    {
        isSelected = false;
        if (isSelecting)
        {
            isSelecting = false;
            isSelected = true;
            mouseDrag = mouseDrag with { X2 = ev.X, Y2 = ev.Y + FirstIndex };
            Log.Info($"Mouse Stop Drag: {mouseDrag}");
            SetNeedsDisplay();
            MouseDragged?.Invoke(mouseDrag);
        }
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
        if (!IsShowCursor || IsHideCursor || !IsFocus)
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

        if (newFirst < 0)
        {
            newFirst = 0;
        }
        if (newFirst + ViewHeight >= TotalCount)
        {
            newFirst = TotalCount - ViewHeight;
        }
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

        FirstIndex = newFirst;
        CurrentIndex = newCurrent;

        SetNeedsDisplay();
    }

    internal void MoveToTop() => Move(-FirstIndex);

    internal void SetIndex(int y)
    {
        int currentY = CurrentIndex - FirstIndex;

        Move(y - currentY);
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

        var x = Math.Max(ViewWidth - 1, 0);
        for (int i = sbStart; i <= sbEnd; i++)
        {
            Move(x, i + ContentY);
            Driver.SetAttribute(Color.Magenta);
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
