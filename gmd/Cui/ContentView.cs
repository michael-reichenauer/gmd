using gmd.Cui.Common;
using Terminal.Gui;


namespace gmd.Cui;

public delegate void DrawContentCallback(int firstIndex, int count, int currentIndex, int width);
public delegate void OnKeyCallback();


class ContentView : View
{
    readonly DrawContentCallback onDrawRepoContent;
    readonly Dictionary<Key, OnKeyCallback> keys = new Dictionary<Key, OnKeyCallback>();

    static readonly int cursorWidth = 1;
    readonly bool isMoveUpDownWrap = false;  // Not used yet

    int totalRowCount = 0;
    int firstIndex = 0;

    internal event Action? CurrentIndexChange;
    int currentIndex = 0;
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

    internal bool IsNoCursor { get; set; } = false;
    internal int ContentX => IsNoCursor ? 0 : cursorWidth;

    internal Point CurrentPoint => new Point(0, firstIndex + CurrentIndex);

    internal ContentView(DrawContentCallback onDrawRepoContent)
    {
        this.onDrawRepoContent = onDrawRepoContent;
    }

    internal void RegisterKeyHandler(Key key, OnKeyCallback callback)
    {
        keys[key] = callback;
    }

    internal void ScrollToShowIndex(int index)
    {
        if (index >= firstIndex && index <= firstIndex + ContentHeight)
        {
            // index already shown
            return;
        }

        int scroll = index - firstIndex;
        Scroll(scroll);
    }


    internal int ContentWidth => Frame.Width - (cursorWidth + ContentX);
    int ViewHeight => Frame.Height;
    internal int ViewWidth => Frame.Width;
    int ContentHeight => Frame.Height;

    int TotalRows => totalRowCount;


    internal void TriggerUpdateContent(int totalCount)
    {
        this.totalRowCount = totalCount;
        if (firstIndex > totalCount)
        {
            firstIndex = totalCount - 1;
        }
        if (CurrentIndex < firstIndex)
        {
            CurrentIndex = firstIndex;
        }
        if (CurrentIndex > firstIndex + ContentHeight)
        {
            CurrentIndex = firstIndex + ContentHeight - 1;
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
                Move(-Math.Max(0, TotalRows));
                return true;
            case Key.End:
                Move(Math.Max(0, TotalRows));
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

        var count = Math.Min(bounds.Height, TotalRows - firstIndex);

        onDrawRepoContent(firstIndex, count, CurrentIndex, ContentWidth);

        DrawCursor();
        DrawVerticalScrollbar();
    }

    void DrawCursor()
    {
        if (IsNoCursor)
        {
            return;
        }

        Move(0, CurrentIndex - firstIndex);
        Driver.SetAttribute(TextColor.White);
        Driver.AddStr("┃");
    }


    void Scroll(int scroll)
    {
        if (TotalRows == 0)
        {   // Cannot scroll empty view
            return;
        }

        int newFirst = firstIndex + scroll;

        if (newFirst < 0)
        {
            newFirst = 0;
        }
        if (newFirst + ViewHeight >= TotalRows)
        {
            newFirst = TotalRows - ViewHeight;
        }
        if (newFirst == firstIndex)
        {   // No move, reached top or bottom
            return;
        }

        int newCurrent = CurrentIndex + (newFirst - firstIndex);

        if (newCurrent < newFirst)
        {   // Need to scroll view up to the new current line
            newCurrent = newFirst;
        }
        if (newCurrent >= newFirst + ContentHeight)
        {   // Need to scroll view down to the new current line
            newCurrent = newFirst - ContentHeight - 1;
        }

        firstIndex = newFirst;
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
        if (TotalRows == 0)
        {   // Cannot scroll empty view
            return;
        }

        int newCurrent = CurrentIndex + move;

        if (newCurrent < 0)
        {   // Reached top, wrap or stay
            newCurrent = isMoveUpDownWrap ? TotalRows - 1 : 0;
        }

        if (newCurrent >= TotalRows)
        {   // Reached bottom, wrap or stay 
            newCurrent = isMoveUpDownWrap ? 0 : TotalRows - 1;
        }

        if (newCurrent == CurrentIndex)
        {   // No move, reached top or bottom
            return;
        }

        CurrentIndex = newCurrent;

        if (CurrentIndex < firstIndex)
        {   // Need to scroll view up to the new current line 
            firstIndex = CurrentIndex;
        }

        if (CurrentIndex >= firstIndex + ViewHeight)
        {  // Need to scroll view down to the new current line
            firstIndex = CurrentIndex - ViewHeight + 1;
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
        if (TotalRows == 0 || ViewHeight == TotalRows)
        {   // No need for a scrollbar
            return (0, -1);
        }

        float scrollbarFactor = (float)ViewHeight / (float)TotalRows;

        int sbStart = (int)Math.Floor((float)firstIndex * scrollbarFactor);
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