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

    const int cursorWidth = 1;
    const int verticalScrollbarWidth = 1;
    const int contentMargin = cursorWidth + verticalScrollbarWidth;
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

    internal bool IsNoCursor { get; set; } = false;
    internal int ContentX => IsNoCursor ? 0 : cursorWidth; // !!!!!!!!   remove
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


    internal int ContentWidth => Frame.Width - contentMargin;
    int ViewHeight => Frame.Height;
    int ContentHeight => Frame.Height;
    internal int ViewWidth => Frame.Width;


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

        var drawCount = Math.Min(bounds.Height, Count - FirstIndex);

        if (onDrawContent != null)
        {
            onDrawContent(FirstIndex, drawCount, CurrentIndex, ContentWidth);
        }
        else if (onGetContent != null)
        {
            var rows = onGetContent(FirstIndex, drawCount, CurrentIndex, ContentWidth);
            int y = 0;
            foreach (var row in rows)
            {
                row.Draw(this, ContentX, y++);
            }
        }

        DrawCursor();
        DrawVerticalScrollbar();
    }


    void DrawCursor()
    {
        if (IsNoCursor)
        {
            return;
        }

        Move(0, CurrentIndex - FirstIndex);
        Driver.SetAttribute(TextColor.White);
        Driver.AddStr("┃");
    }


    void Scroll(int scroll)
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

    void Move(int move)
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

        for (int i = sbStart; i <= sbEnd; i++)
        {
            Move(Math.Max(ViewWidth - 1, 0), i);
            Driver.SetAttribute(TextColor.Magenta);
            Driver.AddStr("┃");
        }
    }

    (int, int) GetVerticalScrollbarIndexes()
    {
        if (Count == 0 || ViewHeight == Count)
        {   // No need for a scrollbar
            return (0, -1);
        }

        float scrollbarFactor = (float)ViewHeight / (float)Count;

        int sbStart = (int)Math.Floor((float)FirstIndex * scrollbarFactor);
        int sbSize = (int)Math.Ceiling((float)ViewHeight * scrollbarFactor);

        if (sbStart + sbSize + 1 > ViewHeight)
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