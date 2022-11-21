using Terminal.Gui;


namespace gmd.Cui.Common;

internal delegate void DrawContentCallback(int firstIndex, int count, int currentIndex, int width);

internal delegate IEnumerable<Text> GetContentCallback(int firstIndex, int count, int currentIndex, int width);

internal delegate void OnKeyCallback();


class ContentView : View
{
    readonly DrawContentCallback? onDrawContent;
    readonly GetContentCallback? onGetContent;
    readonly Dictionary<Key, OnKeyCallback> keys = new Dictionary<Key, OnKeyCallback>();

    const int topBorderHeight = 1;
    const int cursorWidth = 1;
    const int verticalScrollbarWidth = 1;
    const int contentXMargin = cursorWidth + verticalScrollbarWidth;
    readonly bool isMoveUpDownWrap = false;  // Not used yet
    int currentIndex = 0;

    internal ContentView(DrawContentCallback onDrawContent)
    {
        this.onDrawContent = onDrawContent;
    }

    internal ContentView(GetContentCallback onGetContent)
    {
        this.onGetContent = onGetContent;
    }


    internal bool IsFocus { get; set; } = true;
    internal int FirstIndex { get; private set; } = 0;
    internal int Count { get; private set; } = 0;

    internal int CurrentIndex
    {
        get { return currentIndex; }
        private set
        {
            var v = currentIndex;
            currentIndex = value;
            if (v != value)
            {
                CurrentIndexChange?.Invoke();
            }
        }
    }

    internal event Action? CurrentIndexChange;

    int ViewHeight => Frame.Height;
    internal int ViewWidth => Frame.Width;
    internal bool IsNoCursor { get; set; } = false;
    internal bool IsTopBorder { get; set; } = false;
    internal bool IsHideCursor { get; set; } = false;
    internal int ContentX => IsNoCursor ? 0 : cursorWidth;
    internal int ContentY => IsTopBorder ? topBorderHeight : 0;
    internal int ContentWidth => Frame.Width - ContentX - verticalScrollbarWidth;
    internal int ContentHeight => IsTopBorder ? ViewHeight - topBorderHeight : ViewHeight;
    internal Point CurrentPoint => new Point(0, FirstIndex + CurrentIndex);


    internal void RegisterKeyHandler(Key key, OnKeyCallback callback)
    {
        keys[key] = callback;
    }

    internal void ScrollToShowIndex(int index)
    {
        if (index >= FirstIndex && index <= FirstIndex + ContentHeight)
        {
            // index already shown
            return;
        }

        int scroll = index - FirstIndex;
        Scroll(scroll);
    }


    internal void TriggerUpdateContent(int totalCount)
    {
        this.Count = totalCount;
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
        if (!IsFocus)
        {
            return false;
        }

        switch (keyEvent.Key)
        {
            case Key.Esc:
                return false;
            case Key.CursorUp:
                Move(-1);
                return true;
            case Key.PageUp:
                Move(-Math.Max(0, ContentHeight - 1));
                return true;
            case Key.CursorDown:
                Move(1);
                return true;
            case Key.PageDown:
                Move(Math.Max(0, ContentHeight - 1));
                return true;
            case Key.Home:
                Move(-Math.Max(0, Count));
                return true;
            case Key.End:
                Move(Math.Max(0, Count));
                return true;
        }

        if (keys.TryGetValue(keyEvent.Key, out var callback))
        {
            callback();
            return true;
        }

        return false;
    }

    public override bool MouseEvent(MouseEvent ev)
    {
        // Log.Info($"Mouse: {ev}");
        // if (ev.Flags.HasFlag(MouseFlags.WheeledDown))
        // {
        //     Log.Info("Scroll down");
        //     Scroll(1);
        // }
        // if (ev.Flags.HasFlag(MouseFlags.WheeledUp))
        // {
        //     Log.Info("Scroll upp");
        //     Scroll(-1);
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
        var drawCount = Math.Min(ContentHeight, Count - FirstIndex);

        if (onDrawContent != null)
        {
            onDrawContent(FirstIndex, drawCount, CurrentIndex, ContentWidth);
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

    void DrawTopBorder()
    {
        if (!IsTopBorder)
        {
            return;
        }
        Move(0, 0);
        if (IsFocus)
        {
            Driver.SetAttribute(TextColor.White);
            Driver.AddStr(new string('━', ViewWidth));
        }
        else
        {
            Driver.SetAttribute(TextColor.Dark);
            Driver.AddStr(new string('─', ViewWidth));
        }
    }


    void DrawCursor()
    {
        if (IsNoCursor || IsHideCursor || !IsFocus)
        {
            return;
        }

        Move(0, ContentY + (CurrentIndex - FirstIndex));
        Driver.SetAttribute(TextColor.White);
        Driver.AddStr("┃");
    }


    internal void Scroll(int scroll)
    {
        if (Count == 0)
        {   // Cannot scroll empty view
            return;
        }

        int newFirst = FirstIndex + scroll;

        if (newFirst < 0)
        {
            newFirst = 0;
        }
        if (newFirst + ViewHeight >= Count)
        {
            newFirst = Count - ViewHeight;
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

    internal void Move(int move)
    {
        if (IsNoCursor)
        {
            Scroll(move);
            return;
        }

        // Log.Info($"move {move}, current: {currentIndex}");
        if (Count == 0)
        {   // Cannot scroll empty view
            return;
        }

        int newCurrent = CurrentIndex + move;

        if (newCurrent < 0)
        {   // Reached top, wrap or stay
            newCurrent = isMoveUpDownWrap ? Count - 1 : 0;
        }

        if (newCurrent >= Count)
        {   // Reached bottom, wrap or stay 
            newCurrent = isMoveUpDownWrap ? 0 : Count - 1;
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

        SetNeedsDisplay();
    }

    void DrawVerticalScrollbar()
    {
        (int sbStart, int sbEnd) = GetVerticalScrollbarIndexes();

        var x = Math.Max(ViewWidth - 1, 0);
        for (int i = sbStart; i <= sbEnd; i++)
        {
            Move(x, i + ContentY);
            Driver.SetAttribute(TextColor.Magenta);
            Driver.AddStr("┃");
        }
    }

    (int, int) GetVerticalScrollbarIndexes()
    {
        if (Count == 0 || Count <= ContentHeight)
        {   // No need for a scrollbar
            return (0, -1);
        }

        float scrollbarFactor = (float)ContentHeight / (float)Count;

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