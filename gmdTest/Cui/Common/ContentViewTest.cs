using gmd.Cui.Common;
using Terminal.Gui;

namespace gmdTest.Cui.Common;

// The view side of ContentScroll and ContentSelection, i.e. that the view passes its own height on
// to the index math and acts on what it answers. Drawing needs a Terminal.Gui driver and is not
// covered here, but a view built from a list of rows can be moved and scrolled without one, as
// long as its Frame is set, since that is where its height comes from.
[TestClass]
public class ContentViewTest
{
    [TestMethod]
    public void TestRowsGivenToTheConstructorAreCounted()
    {
        var view = NewView(100);

        Assert.AreEqual(100, view.TotalCount);
        Assert.AreEqual(0, view.FirstIndex);
        Assert.AreEqual(0, view.CurrentIndex);
    }

    [TestMethod]
    public void TestMoveScrollsWhenTheCursorLeavesTheView()
    {
        var view = NewView(100);

        view.Move(1);
        Assert.AreEqual(1, view.CurrentIndex);
        Assert.AreEqual(0, view.FirstIndex); // Still within the 10 rows of the view

        view.Move(20);
        Assert.AreEqual(21, view.CurrentIndex);
        Assert.AreEqual(12, view.FirstIndex); // The cursor row is now the bottom row of the view
    }

    [TestMethod]
    public void TestScrollTakesTheCursorWithIt()
    {
        var view = NewView(100);
        view.Move(20); // Cursor on row 20, i.e. the bottom row of the view, which shows 11-20

        view.Scroll(5);

        Assert.AreEqual(25, view.CurrentIndex);
        Assert.AreEqual(16, view.FirstIndex);
    }

    [TestMethod]
    public void TestMoveToTopShowsTheFirstRowWhenTheCursorIsOnTheTopRow()
    {
        var view = NewView(100);
        view.Scroll(50); // Scrolling keeps the cursor on the top row of the view

        view.MoveToTop();

        Assert.AreEqual(0, view.FirstIndex);
        Assert.AreEqual(0, view.CurrentIndex);
    }

    // MoveToTop() used to move the cursor up by the number of rows the view was scrolled down
    // instead of to the first row, so with the cursor further down the view it stopped exactly that
    // many rows short, leaving the rows above out of sight. That is what the filter dialog calls to
    // show a new set of results from the top.
    [TestMethod]
    public void TestMoveToTopShowsTheFirstRowWhenTheCursorIsFurtherDownTheView()
    {
        var view = NewView(100);
        view.Move(50); // Cursor on row 50, i.e. the bottom row of the view, which shows 41-50

        view.MoveToTop();

        Assert.AreEqual(0, view.FirstIndex);
        Assert.AreEqual(0, view.CurrentIndex);
    }

    [TestMethod]
    public void TestMoveToTopShowsTheFirstRowInScrollMode()
    {
        var view = NewView(100);
        view.IsScrollMode = true;
        view.Move(50);

        view.MoveToTop();

        Assert.AreEqual(0, view.FirstIndex);
        Assert.AreEqual(0, view.CurrentIndex);
    }

    // In scroll mode the cursor does not move on its own, the rows do, which is what a view with
    // no cursor (the commit details view, the help dialog) uses.
    [TestMethod]
    public void TestMoveScrollsTheRowsInScrollMode()
    {
        var view = NewView(100);
        view.IsScrollMode = true;

        view.Move(4);

        Assert.AreEqual(4, view.FirstIndex);
        Assert.AreEqual(4, view.CurrentIndex);
    }

    [TestMethod]
    public void TestSetIndexAtViewYPutsTheCursorOnAClickedRow()
    {
        var view = NewView(100);
        view.Scroll(30);

        view.SetIndexAtViewY(4); // The fifth row of the view was clicked

        Assert.AreEqual(34, view.CurrentIndex);
        Assert.AreEqual(30, view.FirstIndex);
    }

    [TestMethod]
    public void TestCurrentIndexChangeIsRaisedForTheViewsCursor()
    {
        var view = NewView(100);
        var count = 0;
        view.CurrentIndexChange += () => count++;

        view.Move(1);
        view.SetCurrentIndex(50);

        Assert.AreEqual(2, count);
    }

    // A top border is drawn on the first row of the view, leaving one row less for the content.
    [TestMethod]
    public void TestTopBorderTakesTheFirstRowOfTheView()
    {
        var view = NewView(100);

        Assert.AreEqual(10, view.ContentHeight);
        Assert.AreEqual(0, view.ContentY);

        view.IsTopBorder = true;

        Assert.AreEqual(9, view.ContentHeight);
        Assert.AreEqual(1, view.ContentY);
    }

    // The cursor is drawn in a margin to the left of the content, and the scrollbar in one to the
    // right, so both are taken off the width the content is asked to fill.
    [TestMethod]
    public void TestCursorMarginTakesTheFirstColumnOfTheView()
    {
        var view = NewView(100);

        Assert.AreEqual(0, view.ContentX);
        Assert.AreEqual(19, view.ContentWidth); // 20 wide, less the scrollbar

        view.IsCursorMargin = true;

        Assert.AreEqual(1, view.ContentX);
        Assert.AreEqual(18, view.ContentWidth);
    }

    static ContentView NewView(int rowCount, int width = 20, int height = 10)
    {
        var rows = Enumerable.Range(0, rowCount).Select(i => Text.White($"row {i}").ToText()).ToList();
        return new ContentView(rows) { Frame = new Rect(0, 0, width, height) };
    }
}
