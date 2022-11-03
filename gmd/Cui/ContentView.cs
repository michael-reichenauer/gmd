using gmd.ViewRepos;
using Terminal.Gui;


namespace gmd.Cui;

public delegate void DrawContent(int width, int Height, int firstIndex, int currentIndex);

class ContentView : View
{
    private readonly DrawContent onDrawRepoContent;

    static readonly int cursorWidth = 1;
    readonly bool isMoveUpDownWrap = false;  // Not used yet

    internal readonly int ContentX = cursorWidth;

    int totalRowCount = 0;
    int firstIndex = 0;
    int currentIndex = 0;

    internal ContentView(DrawContent onDrawRepoContent)
    {
        //repoLayout = new RepoLayout(this, contentX);
        this.onDrawRepoContent = onDrawRepoContent;
    }

    int ViewHeight => Frame.Height;
    int ViewWidth => Frame.Width;
    int ContentWidth => Frame.Width - (cursorWidth + ContentX);
    int ContentHeight => Frame.Height;

    int TotalRows => totalRowCount;


    internal void TriggerUpdateContent(int rowCount)
    {
        this.totalRowCount = rowCount;
        if (firstIndex > rowCount)
        {
            firstIndex = rowCount;
        }
        if (currentIndex < firstIndex)
        {
            currentIndex = firstIndex;
        }
        if (currentIndex > firstIndex + ContentHeight)
        {
            currentIndex = firstIndex + ContentHeight;
        }

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
            case Key.CursorDown:
                Move(1);
                return true;
            default:
                Log.Info($"Key {keyEvent}");
                return true;
        }
    }

    public override bool MouseEvent(MouseEvent ev)
    {
        //  Log.Info($"Mouse: {ev}");
        if (ev.Flags.HasFlag(MouseFlags.WheeledDown))
        {
            Scroll(1);
        }
        if (ev.Flags.HasFlag(MouseFlags.WheeledUp))
        {
            Scroll(-1);
        }

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

        onDrawRepoContent(ContentWidth, ContentHeight, firstIndex, currentIndex);

        DrawCursor();
        DrawVerticalScrollbar();
    }

    void DrawCursor()
    {
        Move(0, currentIndex);
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

        // int newCurrent = currentIndex + (newFirst - firstIndex);

        // if (newCurrent < newFirst)
        // {   // Need to scroll view up to the new current line
        //     newCurrent = newFirst;
        // }
        // if (newCurrent >= newFirst + Rows)
        // {   // Need to scroll view down to the new current line
        //     newCurrent = newFirst - Rows - 1;
        // }

        firstIndex = newFirst;
        //currentIndex = newCurrent;

        SetNeedsDisplay();
    }

    void Move(int move)
    {
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