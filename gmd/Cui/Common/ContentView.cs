using Terminal.Gui;


namespace gmd.Cui.Common;


internal delegate IEnumerable<Text> GetContentCallback(int firstIndex, int count, int currentIndex, int contentWidth);
internal delegate void OnKeyCallback();
internal delegate void OnMouseCallback(int x, int y);


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

    int currentIndex = 0;
    int mouseEventX = -1;
    int mouseEventY = -1;
    int selectStartIndex = -1;
    int selectEndIndex = -1;


    internal ContentView(GetContentCallback onGetContent)
    {
        this.onGetContent = onGetContent;
    }

    internal ContentView(IReadOnlyList<Text> content)
    {
        this.content = content;
        this.TriggerUpdateContent(this.content.Count);
    }

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

    public event Action? CurrentIndexChange;

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
        // Log.Info($"Mouse: {ev}, {ev.OfX}, {ev.OfY}, {ev.X}, {ev.Y}");

        // On linux (at least dev container console), there is a bug that sends same last mouse event
        // whenever mouse is moved, to still support scroll, we check mouse position.
        bool isSamePos = (ev.X == mouseEventX && ev.Y == mouseEventY);
        mouseEventX = ev.X;
        mouseEventY = ev.Y;

        if (ev.Flags.HasFlag(MouseFlags.WheeledDown) && isSamePos)
        {
            Scroll(1);
            return true;
        }
        else if (ev.Flags.HasFlag(MouseFlags.WheeledUp) && isSamePos)
        {
            Scroll(-1);
            return true;
        }

        if (Build.IsWindows)
        {
            if (mouses.TryGetValue(ev.Flags, out var callback))
            {
                callback(ev.X, ev.Y);
                return true;
            }
        }


        // if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        // {
        //     if (ev.Flags.HasFlag(MouseFlags.WheeledDown))
        //     {
        //         Scroll(1);
        //     }
        //     if (ev.Flags.HasFlag(MouseFlags.WheeledUp))
        //     {
        //         Scroll(-1);
        //     }
        // }

        return false;
        // if (!ev.Flags.HasFlag(MouseFlags.Button1Clicked) && !ev.Flags.HasFlag(MouseFlags.Button1Pressed)
        //     && !ev.Flags.HasFlag(MouseFlags.Button1Pressed | MouseFlags.ReportMousePosition)
        //     && !ev.Flags.HasFlag(MouseFlags.Button1Released)
        //     && !ev.Flags.HasFlag(MouseFlags.Button1Pressed | MouseFlags.ButtonShift)
        //     && !ev.Flags.HasFlag(MouseFlags.WheeledDown) && !ev.Flags.HasFlag(MouseFlags.WheeledUp)
        //     && !ev.Flags.HasFlag(MouseFlags.Button1DoubleClicked)
        //     && !ev.Flags.HasFlag(MouseFlags.Button1DoubleClicked | MouseFlags.ButtonShift)
        //     && !ev.Flags.HasFlag(MouseFlags.Button1TripleClicked))
        // {
        //     return false;
        // }


        // return false;
    }

    public override void Redraw(Rect bounds)
    {
        Clear();

        var topMargin = IsTopBorder ? topBorderHeight : 0;
        var drawCount = Math.Min(ContentHeight, TotalCount - FirstIndex);

        if (content != null)
        {
            int y = ContentY;
            content.Skip(FirstIndex).Take(drawCount).ForEach(row => row.Draw(this, ContentX, y++));
        }
        else if (onGetContent != null)
        {
            var rows = onGetContent(FirstIndex, drawCount, CurrentIndex, ContentWidth);
            int y = ContentY;
            rows.ForEach(row => row.Draw(this, ContentX, y++));
        }

        DrawTopBorder();
        DrawCursor();
        DrawVerticalScrollbar();
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