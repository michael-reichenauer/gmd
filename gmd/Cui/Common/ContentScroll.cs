namespace gmd.Cui.Common;

// Where a ContentView is scrolled to: the first shown row, the row the cursor is on, and how many
// rows the content has. This is that state and the index math of moving it, i.e. no drawing and no
// Terminal.Gui, so it can be tested without a terminal. The view side is in ContentView, which
// redraws whenever one of the mutating methods here reports that something actually moved.
//
// The view height is read through callbacks rather than stored, since a view is resized while it
// is shown. ViewHeight is the whole view and ContentHeight is what is left of it when the view
// draws a top border.
class ContentScroll
{
    readonly Func<int> getViewHeight;
    readonly Func<int> getContentHeight;

    readonly bool isMoveUpDownWrap = false; // Not used yet

    int currentIndex = 0;

    internal ContentScroll(Func<int> getViewHeight, Func<int> getContentHeight)
    {
        this.getViewHeight = getViewHeight;
        this.getContentHeight = getContentHeight;
    }

    public event Action? CurrentIndexChange;

    public int FirstIndex { get; private set; } = 0;
    public int TotalCount { get; private set; } = 0;

    public int CurrentIndex
    {
        get { return currentIndex; }
        private set
        {
            var v = currentIndex;
            currentIndex = value;
            if (v != value)
                CurrentIndexChange?.Invoke();
        }
    }

    int ViewHeight => getViewHeight();
    int ContentHeight => getContentHeight();

    public void SetTotalCount(int totalCount) => TotalCount = totalCount;

    public void SetCurrentIndex(int index) => CurrentIndex = index;

    // Scrolls so that the index is shown, unless it already is with at least the margin of rows
    // above and below it. Returns true if the view scrolled.
    public bool ScrollToShowIndex(int index, int margin = 5)
    {
        if (index >= FirstIndex + margin && index <= FirstIndex + ContentHeight - margin)
        {
            // index already shown
            return false;
        }

        int scroll = index - FirstIndex - 5;
        return Scroll(scroll);
    }

    // Scrolls the rows, taking the cursor with it. Returns true if the view scrolled, i.e. false
    // when the top or the bottom was already reached.
    public bool Scroll(int scroll)
    {
        if (TotalCount == 0)
        { // Cannot scroll empty view
            return false;
        }

        int newFirst = FirstIndex + scroll;

        if (newFirst < 0)
            newFirst = 0;

        if (newFirst + ViewHeight >= TotalCount)
        {
            newFirst = TotalCount - ViewHeight;
        }
        if (newFirst < 0)
            newFirst = 0;
        if (newFirst == FirstIndex)
        { // No move, reached top or bottom
            return false;
        }

        int newCurrent = CurrentIndex + (newFirst - FirstIndex);

        if (newCurrent < newFirst)
        { // Need to scroll view up to the new current line
            newCurrent = newFirst;
        }
        if (newCurrent >= newFirst + ContentHeight)
        { // Need to scroll view down to the new current line
            newCurrent = newFirst - ContentHeight - 1;
        }
        if (newCurrent < 0)
            newCurrent = 0;

        FirstIndex = newFirst;
        CurrentIndex = newCurrent;

        return true;
    }

    // Moves the cursor, scrolling the rows if it would move outside the view. Returns true if the
    // cursor moved, i.e. false when the top or the bottom was already reached.
    public bool Move(int move)
    {
        // Log.Info($"move {move}, current: {currentIndex}");
        if (TotalCount == 0)
        { // Cannot scroll empty view
            return false;
        }

        int newCurrent = CurrentIndex + move;

        if (newCurrent < 0)
        { // Reached top, wrap or stay
            newCurrent = isMoveUpDownWrap ? TotalCount - 1 : 0;
        }

        if (newCurrent >= TotalCount)
        { // Reached bottom, wrap or stay
            newCurrent = isMoveUpDownWrap ? 0 : TotalCount - 1;
        }

        if (newCurrent == CurrentIndex)
        { // No move, reached top or bottom
            return false;
        }

        CurrentIndex = newCurrent;

        if (CurrentIndex < FirstIndex)
        { // Need to scroll view up to the new current line
            FirstIndex = CurrentIndex;
        }

        if (CurrentIndex >= FirstIndex + ViewHeight)
        { // Need to scroll view down to the new current line
            FirstIndex = CurrentIndex - ViewHeight + 1;
        }
        // Log.Info($"move {move}, current: {currentIndex}, first: {FirstIndex}");

        return true;
    }

    // Called when the content has shrunk to fewer rows than the first shown row, i.e. when the
    // view would show nothing: move to the end of the rows that are left.
    public void MoveToEndOfContent()
    {
        FirstIndex = Math.Max(0, TotalCount - 3);
        CurrentIndex = TotalCount - 1;
    }

    // The first and last row of the vertical scrollbar, where the last is before the first when
    // all rows fit in the view, i.e. when no scrollbar is needed.
    public (int, int) GetVerticalScrollbarIndexes()
    {
        if (TotalCount == 0 || TotalCount <= ContentHeight)
        { // No need for a scrollbar
            return (0, -1);
        }

        float scrollbarFactor = (float)ContentHeight / (float)TotalCount;

        int sbStart = (int)Math.Floor((float)FirstIndex * scrollbarFactor);
        int sbSize = (int)Math.Ceiling((float)ContentHeight * scrollbarFactor);

        if (sbStart + sbSize + 1 > ContentHeight)
        {
            sbStart = ViewHeight - sbSize - 1;
            if (sbStart < 0)
            {
                sbStart = 0;
            }
        }

        return (sbStart, sbStart + sbSize);
    }
}
