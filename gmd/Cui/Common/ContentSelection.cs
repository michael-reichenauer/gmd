namespace gmd.Cui.Common;

record class Selection(int X1, int I1, int X2, int I2, int InitialIndex)
{
    public bool IsEmpty => X1 == X2 && I1 == I2;
}

// The selected region of a ContentView: the selected rows, and the selected columns of the row
// when the selection is within one row. This is that state and the index math of selecting with
// shift+up/down and by dragging the mouse, i.e. no drawing and no Terminal.Gui, so it can be
// tested without a terminal. The view side is in ContentView, which moves the cursor and scrolls
// when the methods here report that the selection was extended past the row the cursor is on.
class ContentSelection
{
    Selection selection = new Selection(0, 0, 0, 0, 0);
    bool isSelected = false;

    // Where the mouse was at the previous drag event, which is what tells a drag left from a drag
    // right, i.e. whether the selection is being extended or shrunk.
    int lastMouseX = 0;
    int lastMouseIndex = 0;

    public Selection Selection => selection;
    public bool IsSelected => isSelected;
    public int StartIndex => selection.I1;
    public int Count => isSelected ? selection.I2 - selection.I1 + 1 : 0;

    public bool IsRowSelected(int index) => isSelected && index >= selection.I1 && index <= selection.I2;

    // Clears the selection. Returns true if there was one, i.e. if the view needs a redraw.
    public bool Clear()
    {
        if (!isSelected)
            return false;

        isSelected = false;
        selection = new Selection(0, 0, 0, 0, 0);
        return true;
    }

    // Extends the selection one row up, or shrinks it when the cursor is at its bottom. Returns
    // true if the cursor should move up as well, i.e. false when the selection was just started or
    // the top is already reached.
    public bool SelectUp(int currentIndex)
    {
        if (!isSelected)
        { // Start selection of current row
            isSelected = true;
            selection = new Selection(0, currentIndex, int.MaxValue, currentIndex, currentIndex);
            return false;
        }

        if (currentIndex == 0)
        { // Already at top of page, no need to move
            return false;
        }

        if (currentIndex <= selection.I1)
        { // Expand selection upp
            selection = selection with { I1 = currentIndex - 1 };
        }
        else
        { // Shrink selection upp
            selection = selection with { I2 = currentIndex - 1 };
        }

        return true;
    }

    // Extends the selection one row down, or shrinks it when the cursor is at its top. Returns
    // true if the cursor should move down as well, i.e. false when the selection was just started
    // or the bottom is already reached.
    public bool SelectDown(int currentIndex, int totalCount)
    {
        if (!isSelected)
        { // Start selection of current row
            isSelected = true;
            selection = new Selection(0, currentIndex, int.MaxValue, currentIndex, currentIndex);
            return false;
        }

        if (currentIndex >= totalCount - 1)
        { // Already at bottom of page, no need to move
            return false;
        }

        if (currentIndex >= selection.I2)
        { // Expand selection down
            selection = selection with { I2 = currentIndex + 1 };
        }
        else if (currentIndex <= selection.I1)
        { // Shrink selection down
            selection = selection with { I1 = currentIndex + 1 };
        }

        return true;
    }

    // Extends the selection to the column x of the row index i the mouse was dragged to, starting
    // a selection if there is none. Returns which way the drag moved vertically (-1, 0 or 1), so
    // the view can scroll when the drag reaches its top or bottom edge.
    public int Drag(int x, int i)
    {
        if (!isSelected)
        { // Start mouse dragging
            isSelected = true;
            selection = new Selection(x, i, x, i, i);
            lastMouseX = x;
            lastMouseIndex = i;
        }

        var x1 = selection.X1;
        var i1 = selection.I1;
        var x2 = selection.X2;
        var i2 = selection.I2;

        if (x < lastMouseX)
        { // Moving left, expand selection on left or shrink selection on right side
            if (x < x1)
                x1 = x;
            else
                x2 = x;
        }
        else if (x > lastMouseX)
        { // Moving right expand selection on right or shrink selection on left side
            if (x > x2)
                x2 = x;
            else
                x1 = x;
        }

        var direction = 0;
        if (i < lastMouseIndex)
        { // Moving upp, expand selection upp or shrink selection on bottom side
            direction = -1;
            if (i < i1)
                i1 = i;
            else
                i2 = i;
        }
        else if (i > lastMouseIndex)
        { // // Moving down, expand selection down or shrink selection on top side
            direction = 1;
            if (i > i2)
                i2 = i;
            else
                i1 = i;
        }

        selection = new Selection(x1, i1, x2, i2, selection.InitialIndex);
        lastMouseX = x;
        lastMouseIndex = i;
        // Log.Info($"Mouse Drag: {selection}");

        return direction;
    }
}
