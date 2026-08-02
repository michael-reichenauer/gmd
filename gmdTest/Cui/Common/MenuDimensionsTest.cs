using gmd.Cui.Common;

namespace gmdTest.Cui.Common;

// Where a menu is drawn and how wide its parts are. The screen is 80x25 in most of these, and the
// menu is the three items below, whose widest text is 'Three' (5) and whose widest shortcut is
// 'bb' (2, plus one for the space before it). Width and Height include the two border characters,
// so the three items give a height of 5.
[TestClass]
public class MenuDimensionsTest
{
    [TestMethod]
    public void TestDimensionsOfASmallMenu()
    {
        var d = Calculate(Three, "Title", 5, 5);

        Assert.AreEqual(5, d.X);
        Assert.AreEqual(5, d.Y);
        Assert.AreEqual(10, d.Width); // 5 text + 3 shortcut + 2 borders
        Assert.AreEqual(5, d.Height); // 3 items + 2 borders
        Assert.AreEqual(5, d.TitleWidth);
        Assert.AreEqual(3, d.ShortcutWidth);
        Assert.AreEqual(0, d.SubMenuMarkerWidth);
    }

    // A title wider than the items widens the menu, and the extra goes to the item text so the
    // shortcuts stay right aligned
    [TestMethod]
    public void TestTitleWiderThanTheItemsWidensTheItemText()
    {
        var d = Calculate(Three, "A very long menu title", 5, 5);

        Assert.AreEqual(27, d.Width); // 22 title + 5
        Assert.AreEqual(22, d.TitleWidth);
        Assert.AreEqual(3, d.ShortcutWidth);
    }

    [TestMethod]
    public void TestCenterPlacesTheMenuInTheMiddleOfTheScreen()
    {
        var d = Calculate(Three, "Title", Menu.Center, Menu.Center);

        Assert.AreEqual(35, d.X); // 80 / 2 - 10 / 2
        Assert.AreEqual(10, d.Y); // 25 / 2 - 5 / 2
    }

    // A menu that would reach past the right edge is moved left by its own width, i.e. it opens to
    // the left of where it was asked for rather than being clipped
    [TestMethod]
    public void TestMenuPastTheRightEdgeIsMovedLeft()
    {
        var d = Calculate(Three, "Title", 75, 5);

        Assert.AreEqual(65, d.X); // 75 - 10
    }

    // A sub menu is opened to the right of its parent, and the parent's x is passed as altX so it
    // can be moved to the left of the parent instead when there is no room to the right
    [TestMethod]
    public void TestSubMenuPastTheRightEdgeUsesTheAlternativeX()
    {
        var d = Calculate(Three, "Title", 75, 5, altX: 60);

        Assert.AreEqual(50, d.X); // 60 - 10
    }

    [TestMethod]
    public void TestMenuPastTheBottomEdgeIsMovedUp()
    {
        var d = Calculate(Three, "Title", 5, 24);

        Assert.AreEqual(20, d.Y); // 25 - 5
    }

    // A sub menu item reserves two columns for its ' >' marker, and a menu of only one letter
    // shortcuts reserves two for those
    [TestMethod]
    public void TestSubMenuItemReservesTheMarkerColumns()
    {
        var d = Calculate([Item("One", "a"), new SubMenu("Sub", "s", Three)], "Title", 5, 5);

        Assert.AreEqual(2, d.SubMenuMarkerWidth);
        Assert.AreEqual(2, d.ShortcutWidth);
        Assert.AreEqual(4, d.TitleWidth);
    }

    // More items than fit are capped by the screen height, which also leaves a column for the
    // scrollbar, and the menu is moved up so it ends at the bottom of the screen
    [TestMethod]
    public void TestMoreItemsThanFitAreCappedByTheScreenHeight()
    {
        var d = Calculate(Enumerable.Range(0, 40).Select(i => Item($"Item {i}")).ToList(), "Title", 5, 5);

        Assert.AreEqual(25, d.Height);
        Assert.AreEqual(0, d.Y);
        Assert.AreEqual(11, d.Width); // 7 text + 1 shortcut + 1 scrollbar + 2 borders
    }

    // Every menu reserves one column for a shortcut even when no item has one, since the width is
    // the longest shortcut plus one for the space before it
    [TestMethod]
    public void TestAShortcutColumnIsReservedEvenWithNoShortcuts()
    {
        var d = Calculate([Item("One"), Item("Two")], "Title", 5, 5);

        Assert.AreEqual(1, d.ShortcutWidth);
    }

    [TestMethod]
    public void TestEmptyMenuIsTheBordersAndTheTitle()
    {
        var d = Calculate([], "Title", 5, 5);

        Assert.AreEqual(2, d.Height);
        Assert.AreEqual(10, d.Width);
        Assert.AreEqual(0, d.ShortcutWidth);
    }

    // Pinned as current behavior: on a screen too narrow for the menu, the item text is floored at
    // 10 columns, which is wider than the view it has to be drawn in. Harmless today, since the
    // rows are clipped by the view, and no terminal gmd is usable in is that narrow.
    [TestMethod]
    public void TestVeryNarrowScreenLeavesTheItemTextWiderThanTheMenu()
    {
        var d = Calculate(Three, "Title", 5, 5, screenWidth: 8);

        Assert.AreEqual(0, d.X);
        Assert.AreEqual(8, d.Width);
        Assert.AreEqual(10, d.TitleWidth);
    }

    static readonly List<MenuItem> Three = [Item("One", "a"), Item("Two", "bb"), Item("Three")];

    static MenuItem Item(string text, string shortcut = "") => new MenuItem(text, shortcut, () => { });

    static MenuDimensions Calculate(
        IReadOnlyList<MenuItem> items,
        string title,
        int x,
        int y,
        int altX = -1,
        int screenWidth = 80,
        int screenHeight = 25
    ) => MenuDimensions.Calculate(items, title, x, y, altX, screenWidth, screenHeight);
}
