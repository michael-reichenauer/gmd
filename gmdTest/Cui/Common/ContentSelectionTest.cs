using gmd.Cui.Common;

namespace gmdTest.Cui.Common;

// What is selected in a ContentView, i.e. the rows that ctrl-c copies and that the commit menu
// acts on. A selection is either whole rows (I1 to I2) or, while it is within one row, the columns
// X1 to X2 of that row.
[TestClass]
public class ContentSelectionTest
{
    [TestMethod]
    public void TestNothingSelectedToStartWith()
    {
        var selection = new ContentSelection();

        Assert.IsFalse(selection.IsSelected);
        Assert.AreEqual(0, selection.Count);
        Assert.IsFalse(selection.IsRowSelected(0));
        Assert.IsFalse(selection.Clear()); // Nothing to clear, so no redraw
    }

    // The first shift+up or shift+down selects the row the cursor is on, and takes the whole row,
    // i.e. all columns from 0 to int.MaxValue.
    [TestMethod]
    public void TestSelectUpStartsBySelectingTheCursorRow()
    {
        var selection = new ContentSelection();

        Assert.IsFalse(selection.SelectUp(5)); // The cursor moves on the caller's side either way

        Assert.IsTrue(selection.IsSelected);
        Assert.AreEqual(new Selection(0, 5, int.MaxValue, 5, 5), selection.Selection);
        Assert.AreEqual(5, selection.StartIndex);
        Assert.AreEqual(1, selection.Count);
        Assert.IsTrue(selection.IsRowSelected(5));
        Assert.IsFalse(selection.IsRowSelected(4));
    }

    [TestMethod]
    public void TestSelectUpExpandsTheSelectionUpwards()
    {
        var selection = new ContentSelection();
        selection.SelectUp(5);

        Assert.IsTrue(selection.SelectUp(5));

        Assert.AreEqual(4, selection.StartIndex);
        Assert.AreEqual(2, selection.Count); // Rows 4-5
    }

    [TestMethod]
    public void TestSelectUpShrinksASelectionMadeDownwards()
    {
        var selection = new ContentSelection();
        selection.SelectDown(5, 100);
        selection.SelectDown(5, 100); // Rows 5-6, cursor on row 6

        Assert.IsTrue(selection.SelectUp(6));

        Assert.AreEqual(5, selection.StartIndex);
        Assert.AreEqual(1, selection.Count); // Back to row 5 only
    }

    [TestMethod]
    public void TestSelectUpAtTheTopRowIsNoMove()
    {
        var selection = new ContentSelection();
        selection.SelectUp(0);

        Assert.IsFalse(selection.SelectUp(0));

        Assert.AreEqual(1, selection.Count);
    }

    [TestMethod]
    public void TestSelectDownStartsBySelectingTheCursorRow()
    {
        var selection = new ContentSelection();

        Assert.IsFalse(selection.SelectDown(5, 100));

        Assert.AreEqual(new Selection(0, 5, int.MaxValue, 5, 5), selection.Selection);
        Assert.AreEqual(1, selection.Count);
    }

    [TestMethod]
    public void TestSelectDownExpandsTheSelectionDownwards()
    {
        var selection = new ContentSelection();
        selection.SelectDown(5, 100);

        Assert.IsTrue(selection.SelectDown(5, 100));

        Assert.AreEqual(5, selection.StartIndex);
        Assert.AreEqual(2, selection.Count); // Rows 5-6
    }

    [TestMethod]
    public void TestSelectDownAtTheLastRowIsNoMove()
    {
        var selection = new ContentSelection();
        selection.SelectDown(99, 100);

        Assert.IsFalse(selection.SelectDown(99, 100));

        Assert.AreEqual(1, selection.Count);
    }

    [TestMethod]
    public void TestClearForgetsTheSelection()
    {
        var selection = new ContentSelection();
        selection.SelectDown(5, 100);

        Assert.IsTrue(selection.Clear()); // There was a selection, so the view needs a redraw

        Assert.IsFalse(selection.IsSelected);
        Assert.AreEqual(0, selection.Count);
        Assert.IsFalse(selection.IsRowSelected(5));
    }

    // These two are what the view does, key press by key press, and are deliberately mirror images
    // of each other: shift+up used to grow the selection by two rows per press and leave the cursor
    // a row below it, since ContentView.ProcessHotKey moved the cursor up after OnSelectUp() had
    // already moved it. Keep them symmetric.
    [TestMethod]
    public void TestShiftUpSelectsOneRowPerKeyPress()
    {
        var selection = new ContentSelection();
        var current = 50;

        current = ShiftUp(selection, current);
        Assert.AreEqual(1, selection.Count); // Row 50
        Assert.AreEqual(50, current); // The cursor is on it

        current = ShiftUp(selection, current);
        Assert.AreEqual(2, selection.Count); // Rows 49-50
        Assert.AreEqual(49, current);

        current = ShiftUp(selection, current);
        Assert.AreEqual(3, selection.Count); // Rows 48-50
        Assert.AreEqual(48, current);
    }

    [TestMethod]
    public void TestShiftDownSelectsOneRowPerKeyPress()
    {
        var selection = new ContentSelection();
        var current = 50;

        current = ShiftDown(selection, current);
        Assert.AreEqual(1, selection.Count); // Row 50
        Assert.AreEqual(50, current); // The cursor is on it

        current = ShiftDown(selection, current);
        Assert.AreEqual(2, selection.Count); // Rows 50-51
        Assert.AreEqual(51, current);

        current = ShiftDown(selection, current);
        Assert.AreEqual(3, selection.Count); // Rows 50-52
        Assert.AreEqual(52, current);
    }

    [TestMethod]
    public void TestDragStartsASelectionWhereItIsPressed()
    {
        var selection = new ContentSelection();

        Assert.AreEqual(0, selection.Drag(3, 10, 100)); // No direction yet, the drag just started

        Assert.IsTrue(selection.IsSelected);
        Assert.AreEqual(new Selection(3, 10, 3, 10, 10), selection.Selection);
        Assert.IsTrue(selection.Selection.IsEmpty); // Nothing between the two ends yet
    }

    // A drag's first event is the first move, a row or a column on from the press, where the
    // selection started: the cell pressed was left out. It starts where the button was pressed now.
    [TestMethod]
    public void TestDragStartsWhereTheButtonWasPressed()
    {
        var selection = new ContentSelection();
        selection.Press(3, 10);

        Assert.AreEqual(1, selection.Drag(4, 11, 100), "Dragged down from the press");

        Assert.AreEqual(new Selection(3, 10, 4, 11, 10), selection.Selection);
    }

    // A press is the start of the next drag only, not of one after a selection was cleared
    [TestMethod]
    public void TestAPressStartsOneDragOnly()
    {
        var selection = new ContentSelection();
        selection.Press(3, 10);
        selection.Drag(4, 11, 100);
        selection.Clear();

        selection.Drag(6, 20, 100);

        Assert.AreEqual(new Selection(6, 20, 6, 20, 20), selection.Selection);
    }

    [TestMethod]
    public void TestDragRightExpandsTheSelectedColumns()
    {
        var selection = new ContentSelection();
        selection.Drag(3, 10, 100);

        Assert.AreEqual(0, selection.Drag(6, 10, 100));

        Assert.AreEqual(new Selection(3, 10, 6, 10, 10), selection.Selection);
        Assert.IsFalse(selection.Selection.IsEmpty);
    }

    [TestMethod]
    public void TestDragLeftBackOverTheStartShrinksAndThenExpandsTheOtherWay()
    {
        var selection = new ContentSelection();
        selection.Drag(3, 10, 100);
        selection.Drag(6, 10, 100);

        selection.Drag(4, 10, 100); // Still right of where the drag started, so the right end moves
        Assert.AreEqual(new Selection(3, 10, 4, 10, 10), selection.Selection);

        selection.Drag(1, 10, 100); // Left of it, so the left end moves
        Assert.AreEqual(new Selection(1, 10, 4, 10, 10), selection.Selection);
    }

    [TestMethod]
    public void TestDragDownSelectsWholeRows()
    {
        var selection = new ContentSelection();
        selection.Drag(3, 10, 100);

        Assert.AreEqual(1, selection.Drag(3, 12, 100)); // Dragged down, so the view may need to scroll

        Assert.AreEqual(10, selection.StartIndex);
        Assert.AreEqual(3, selection.Count); // Rows 10-12
        Assert.IsTrue(selection.IsRowSelected(11));
    }

    [TestMethod]
    public void TestDragUpSelectsTheRowsAbove()
    {
        var selection = new ContentSelection();
        selection.Drag(3, 10, 100);

        Assert.AreEqual(-1, selection.Drag(3, 8, 100));

        Assert.AreEqual(8, selection.StartIndex);
        Assert.AreEqual(3, selection.Count); // Rows 8-10
    }

    [TestMethod]
    public void TestDragBackUpShrinksTheSelectedRows()
    {
        var selection = new ContentSelection();
        selection.Drag(3, 10, 100);
        selection.Drag(3, 14, 100);

        Assert.AreEqual(-1, selection.Drag(3, 12, 100));

        Assert.AreEqual(10, selection.StartIndex);
        Assert.AreEqual(3, selection.Count); // Rows 10-12
    }

    // The row the drag started on, which the repo view keeps drawing as the current row while the
    // selection is dragged around it.
    [TestMethod]
    public void TestInitialIndexStaysTheRowTheDragStartedOn()
    {
        var selection = new ContentSelection();
        selection.Drag(3, 10, 100);
        selection.Drag(5, 14, 100);
        selection.Drag(2, 7, 100);

        Assert.AreEqual(10, selection.Selection.InitialIndex);
    }

    // A small repo leaves empty rows under the log, and a drag between two of them selected rows
    // that are not there, which Ctrl+C then copied and threw on
    [TestMethod]
    public void TestDragBelowTheLastRowSelectsUpToIt()
    {
        var selection = new ContentSelection();
        selection.Drag(3, 6, 5);
        selection.Drag(3, 9, 5);

        Assert.AreEqual(new Selection(3, 4, 3, 4, 4), selection.Selection);
        Assert.AreEqual(4, selection.StartIndex + selection.Count - 1, "Ends at the last row");
    }

    [TestMethod]
    public void TestDragInAnEmptyViewSelectsNothing()
    {
        var selection = new ContentSelection();

        Assert.AreEqual(0, selection.Drag(3, 2, 0));
        Assert.IsFalse(selection.IsSelected);
    }

    // What ContentView does on shift+up: OnSelectUp() moves the cursor when the selection was
    // extended, and that is all.
    static int ShiftUp(ContentSelection selection, int currentIndex)
    {
        if (selection.SelectUp(currentIndex))
            currentIndex--;
        return currentIndex;
    }

    // What ContentView does on shift+down: OnSelectDown() moves the cursor when the selection was
    // extended, and that is all.
    static int ShiftDown(ContentSelection selection, int currentIndex)
    {
        if (selection.SelectDown(currentIndex, 100))
            currentIndex++;
        return currentIndex;
    }
}
