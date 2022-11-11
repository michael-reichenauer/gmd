using gmd.ViewRepos;
using Terminal.Gui;


namespace gmd.Cui;

public delegate void DrawContentCallback(Rect bounds, int firstIndex, int currentIndex);
public delegate void OnKeyCallback();


class ContentView : View
{
    readonly DrawContentCallback onDrawRepoContent;
    readonly Dictionary<Key, OnKeyCallback> keys = new Dictionary<Key, OnKeyCallback>();

    static readonly int cursorWidth = 1;
    readonly bool isMoveUpDownWrap = false;  // Not used yet

    int totalRowCount = 0;
    int firstIndex = 0;
    int currentIndex = 0;

    internal int CurrentIndex => currentIndex;

    internal bool IsNoCursor { get; set; } = false;
    internal int ContentX => IsNoCursor ? 0 : cursorWidth;

    internal Point CurrentPoint => new Point(0, firstIndex + currentIndex);

    internal ContentView(DrawContentCallback onDrawRepoContent)
    {
        this.onDrawRepoContent = onDrawRepoContent;
    }

    internal void RegisterKeyHandler(Key key, OnKeyCallback callback)
    {
        keys[key] = callback;
    }


    internal int ContentWidth => Frame.Width - (cursorWidth + ContentX);
    int ViewHeight => Frame.Height;
    internal int ViewWidth => Frame.Width;
    int ContentHeight => Frame.Height;

    int TotalRows => totalRowCount;


    internal void TriggerUpdateContent(int rowCount)
    {
        this.totalRowCount = rowCount;
        if (firstIndex > rowCount)
        {
            firstIndex = rowCount - 1;
        }
        if (currentIndex < firstIndex)
        {
            currentIndex = firstIndex;
        }
        if (currentIndex > firstIndex + ContentHeight)
        {
            currentIndex = firstIndex + ContentHeight - 1;
        }
        currentIndex = Math.Min(rowCount - 1, currentIndex);
        currentIndex = Math.Max(0, currentIndex);

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

        Rect contentRect = new Rect(ContentX, 0, ContentWidth, ContentHeight);
        onDrawRepoContent(contentRect, firstIndex, currentIndex);

        DrawCursor();
        DrawVerticalScrollbar();
    }

    void DrawCursor()
    {
        if (IsNoCursor)
        {
            return;
        }

        Move(0, currentIndex - firstIndex);
        Driver.SetAttribute(Colors.White);
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

        int newCurrent = currentIndex + (newFirst - firstIndex);

        if (newCurrent < newFirst)
        {   // Need to scroll view up to the new current line
            newCurrent = newFirst;
        }
        if (newCurrent >= newFirst + ContentHeight)
        {   // Need to scroll view down to the new current line
            newCurrent = newFirst - ContentHeight - 1;
        }

        firstIndex = newFirst;
        currentIndex = newCurrent;

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

        int newCurrent = currentIndex + move;

        if (newCurrent < 0)
        {   // Reached top, wrap or stay
            newCurrent = isMoveUpDownWrap ? TotalRows - 1 : 0;
        }

        if (newCurrent >= TotalRows)
        {   // Reached bottom, wrap or stay 
            newCurrent = isMoveUpDownWrap ? 0 : TotalRows - 1;
        }

        if (newCurrent == currentIndex)
        {   // No move, reached top or bottom
            return;
        }

        currentIndex = newCurrent;

        if (currentIndex < firstIndex)
        {   // Need to scroll view up to the new current line 
            firstIndex = currentIndex;
        }

        if (currentIndex >= firstIndex + ViewHeight)
        {  // Need to scroll view down to the new current line
            firstIndex = currentIndex - ViewHeight + 1;
        }

        SetNeedsDisplay();
    }

    void DrawVerticalScrollbar()
    {
        (int sbStart, int sbEnd) = GetVerticalScrollbarIndexes();

        for (int i = sbStart; i <= sbEnd; i++)
        {
            Move(Math.Max(ViewWidth, 0), i);
            Driver.SetAttribute(Colors.Magenta);
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