using gmd.Cui.Common;

namespace gmdTest.Cui.Common;

// Where a ContentView is scrolled to, i.e. the first shown row and the row the cursor is on. The
// view is 10 rows high in most of these, with 100 rows of content, so the numbers are easy to
// follow. A view that draws a top border has one row less for its content than its height, which
// is the difference the last few tests are about.
[TestClass]
public class ContentScrollTest
{
    [TestMethod]
    public void TestEmptyToStartWith()
    {
        var scroll = NewScroll(0);

        Assert.AreEqual(0, scroll.FirstIndex);
        Assert.AreEqual(0, scroll.CurrentIndex);
        Assert.AreEqual(0, scroll.TotalCount);
    }

    [TestMethod]
    public void TestMoveDownMovesTheCursorOnly()
    {
        var scroll = NewScroll(100);

        Assert.IsTrue(scroll.Move(1));

        Assert.AreEqual(1, scroll.CurrentIndex);
        Assert.AreEqual(0, scroll.FirstIndex);
    }

    // No move needs no redraw, which is what the returned bool tells the view.
    [TestMethod]
    public void TestMoveUpAtTopIsNoMove()
    {
        var scroll = NewScroll(100);

        Assert.IsFalse(scroll.Move(-1));

        Assert.AreEqual(0, scroll.CurrentIndex);
    }

    [TestMethod]
    public void TestMoveStopsAtLastRow()
    {
        var scroll = NewScroll(100);

        Assert.IsTrue(scroll.Move(1000));

        Assert.AreEqual(99, scroll.CurrentIndex);
        Assert.AreEqual(90, scroll.FirstIndex); // The last row is drawn at the bottom of the view
        Assert.IsFalse(scroll.Move(1));
    }

    [TestMethod]
    public void TestMoveDownPastTheViewScrollsOneRow()
    {
        var scroll = NewScroll(100);

        Assert.IsTrue(scroll.Move(10)); // The view shows rows 0-9, so row 10 is one row below it

        Assert.AreEqual(10, scroll.CurrentIndex);
        Assert.AreEqual(1, scroll.FirstIndex);
    }

    [TestMethod]
    public void TestMoveUpPastTheViewScrollsToTheCursor()
    {
        var scroll = NewScroll(100);
        scroll.Move(50);
        Assert.AreEqual(41, scroll.FirstIndex);

        Assert.IsTrue(scroll.Move(-20));

        Assert.AreEqual(30, scroll.CurrentIndex);
        Assert.AreEqual(30, scroll.FirstIndex); // The cursor row is now the top row of the view
    }

    [TestMethod]
    public void TestMoveOnEmptyContentIsNoMove()
    {
        var scroll = NewScroll(0);

        Assert.IsFalse(scroll.Move(1));
        Assert.IsFalse(scroll.Scroll(1));
    }

    [TestMethod]
    public void TestCurrentIndexChangeIsRaisedOnEveryChangeOfTheCursorRow()
    {
        var scroll = NewScroll(100);
        var count = 0;
        scroll.CurrentIndexChange += () => count++;

        scroll.Move(1);
        scroll.Move(1);
        scroll.Move(-1000); // Reaches the top
        scroll.Move(-1); // Already at the top, so the cursor row does not change
        scroll.SetCurrentIndex(5);
        scroll.SetCurrentIndex(5); // Same row again

        Assert.AreEqual(4, count);
    }

    [TestMethod]
    public void TestScrollTakesTheCursorWithIt()
    {
        var scroll = NewScroll(100);

        Assert.IsTrue(scroll.Scroll(5));

        Assert.AreEqual(5, scroll.FirstIndex);
        Assert.AreEqual(5, scroll.CurrentIndex);
    }

    [TestMethod]
    public void TestScrollStopsWithTheLastRowAtTheBottomOfTheView()
    {
        var scroll = NewScroll(100);

        Assert.IsTrue(scroll.Scroll(1000));

        Assert.AreEqual(90, scroll.FirstIndex);
        Assert.AreEqual(90, scroll.CurrentIndex);
        Assert.IsFalse(scroll.Scroll(1));
    }

    [TestMethod]
    public void TestScrollUpAtTopIsNoScroll()
    {
        var scroll = NewScroll(100);

        Assert.IsFalse(scroll.Scroll(-1));

        Assert.AreEqual(0, scroll.FirstIndex);
    }

    // The margin is what makes a row count as shown, and it is applied to both ends, so in a view
    // that is only twice the margin high, the one row in the middle of it is all that counts as
    // shown. A short view therefore scrolls on almost every call.
    [TestMethod]
    public void TestScrollToShowIndexDoesNothingWhenTheIndexIsAlreadyShown()
    {
        var scroll = NewScroll(100);

        Assert.IsFalse(scroll.ScrollToShowIndex(5));

        Assert.AreEqual(0, scroll.FirstIndex);
    }

    [TestMethod]
    public void TestScrollToShowIndexPutsTheIndexFiveRowsFromTheTop()
    {
        var scroll = NewScroll(100);

        Assert.IsTrue(scroll.ScrollToShowIndex(50));

        Assert.AreEqual(45, scroll.FirstIndex);
        Assert.AreEqual(45, scroll.CurrentIndex);
    }

    [TestMethod]
    public void TestMoveToEndOfContentShowsTheThreeLastRows()
    {
        var scroll = NewScroll(7);
        scroll.Scroll(5);

        scroll.MoveToEndOfContent();

        Assert.AreEqual(4, scroll.FirstIndex);
        Assert.AreEqual(6, scroll.CurrentIndex);
    }

    [TestMethod]
    public void TestNoScrollbarWhenAllRowsFitInTheView()
    {
        var scroll = NewScroll(10);

        Assert.AreEqual((0, -1), scroll.GetVerticalScrollbarIndexes()); // Ends before it starts
    }

    [TestMethod]
    public void TestScrollbarMovesWithTheFirstShownRow()
    {
        var scroll = NewScroll(100);

        Assert.AreEqual((0, 1), scroll.GetVerticalScrollbarIndexes());

        scroll.Scroll(40);
        Assert.AreEqual((4, 5), scroll.GetVerticalScrollbarIndexes());

        scroll.Scroll(1000); // The last row is now the bottom row of the view
        Assert.AreEqual((8, 9), scroll.GetVerticalScrollbarIndexes());
    }

    // A top border takes one row of the view, and only some of the math knows that: the scrollbar
    // and the margin of ScrollToShowIndex use the content height, while the row Scroll() stops at
    // uses the whole view height. So the last row of a bordered view is reachable by the cursor but
    // is drawn under the bottom of the view.
    [TestMethod]
    public void TestTopBorderTakesOneRowFromTheContent()
    {
        var scroll = NewScroll(100, viewHeight: 10, contentHeight: 9);

        Assert.IsTrue(scroll.Move(1000));

        Assert.AreEqual(99, scroll.CurrentIndex);
        Assert.AreEqual(90, scroll.FirstIndex); // 9 content rows show 90-98, i.e. not the cursor row
    }

    // Pinned as current behavior, not fixed: scrolling while the cursor is on a row below the
    // content height throws the cursor all the way to the first row, since the row it is put on,
    // newFirst - ContentHeight - 1, is negative and clamped to 0 (it reads like a + was meant).
    // Only reachable in a view with a top border, where Move() lets the cursor onto the row that
    // the border pushed out of view, and gmd's one such view hides its cursor.
    [TestMethod]
    public void TestScrollWithTheCursorBelowTheContentPutsItOnTheFirstRow()
    {
        var scroll = NewScroll(100, viewHeight: 10, contentHeight: 9);
        scroll.Move(9); // The 10th row of a view with 9 content rows
        Assert.AreEqual(0, scroll.FirstIndex);

        Assert.IsTrue(scroll.Scroll(1));

        Assert.AreEqual(1, scroll.FirstIndex);
        Assert.AreEqual(0, scroll.CurrentIndex); // Above the shown rows, rather than 9 or 10
    }

    static ContentScroll NewScroll(int totalCount, int viewHeight = 10, int contentHeight = 10)
    {
        var scroll = new ContentScroll(() => viewHeight, () => contentHeight);
        scroll.SetTotalCount(totalCount);
        return scroll;
    }
}
