using gmd.ViewRepos;
using Terminal.Gui;


namespace gmd.Cui;

class RepoContentView : View
{
    IReadOnlyList<Commit> commits = new List<Commit>();
    RepoLayout repoLayout = new RepoLayout();
    ColorText text;

    int firstIndex = 0;
    int currentIndex = 0;
    bool isMoveUpDownWrap = false;

    internal RepoContentView()
    {
        text = new ColorText(this);

    }

    public int Rows => Frame.Height;
    public int Length => Frame.Width;
    public int Total => commits.Count;


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
        int width = bounds.Width;
        int height = bounds.Height;

        int first = Math.Min(firstIndex, commits.Count);
        int count = Math.Min(height, commits.Count - first);

        Clear();
        repoLayout.SetText(commits.Skip(first).Take(count), text);

        DrawCursor();
        DrawVerticalScrollbar();
    }

    private void DrawCursor()
    {
        Move(0, currentIndex);
        Driver.SetAttribute(Colors.White);
        Driver.AddStr("┃");
    }

    internal void ShowCommits(IReadOnlyList<Commit> commits)
    {
        this.commits = commits;
        SetNeedsDisplay();
    }

    void Scroll(int scroll)
    {
        if (Total == 0)
        {   // Cannot scroll empty view
            return;
        }

        int newFirst = firstIndex + scroll;

        if (newFirst < 0)
        {
            newFirst = 0;
        }
        if (newFirst + Rows >= Total)
        {
            newFirst = Total - Rows;
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
        if (commits.Count == 0)
        {   // Cannot scroll empty view
            return;
        }

        int newCurrent = currentIndex + move;

        if (newCurrent < 0)
        {   // Reached top, wrap or stay
            newCurrent = isMoveUpDownWrap ? commits.Count - 1 : 0;
        }

        if (newCurrent >= commits.Count)
        {   // Reached bottom, wrap or stay 
            newCurrent = isMoveUpDownWrap ? 0 : commits.Count - 1;
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

        if (currentIndex >= firstIndex + Rows)
        {  // Need to scroll view down to the new current line
            firstIndex = currentIndex - Rows + 1;
        }

        SetNeedsDisplay();
    }

    void DrawVerticalScrollbar()
    {
        (int sbStart, int sbEnd) = GetVerticalScrollbarIndexes();

        for (int i = sbStart; i <= sbEnd; i++)
        {
            Move(Math.Max(Length, 0), i);
            Driver.SetAttribute(Colors.Magenta);
            Driver.AddStr("┃");
        }
    }

    (int, int) GetVerticalScrollbarIndexes()
    {
        // return (5, 10);
        if (commits.Count == 0 || Rows == commits.Count)
        {   // No need for a scrollbar
            return (0, -1);
        }

        float scrollbarFactor = (float)Rows / (float)commits.Count;

        int sbStart = (int)Math.Floor((float)firstIndex * scrollbarFactor);
        int sbSize = (int)Math.Ceiling((float)Rows * scrollbarFactor);

        if (sbStart + sbSize + 1 > Rows)
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