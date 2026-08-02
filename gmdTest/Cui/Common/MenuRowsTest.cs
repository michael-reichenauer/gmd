using gmd.Cui.Common;

namespace gmdTest.Cui.Common;

// The rows a menu draws its items as. Written as pictures between '|' markers, so the padding that
// right aligns the shortcuts is visible and the expected value has no trailing whitespace of its
// own. The screen is 80x25 unless a test says otherwise.
[TestClass]
public class MenuRowsTest
{
    // The item text padded to the title width, then the shortcut right aligned, then the sub menu
    // marker. A separator is a line, and a separator with text is a line with the text in it.
    [TestMethod]
    public void TestRowsOfAMixedMenu()
    {
        Assert.AreEqual(
            """
            |One          a  |
            |Two         bb  |
            |Sub          s >  |
            |─────────────────|
            |╴Header ────────|
            |No shortcut     |
            """,
            Picture(Mixed, "Title")
        );
    }

    [TestMethod]
    public void TestItemTextTooLongIsTruncatedWithAnEllipsis()
    {
        Assert.AreEqual(
            """
            |A really quite l… q|
            """,
            Picture([Item("A really quite long menu item text", "q")], "T", screenWidth: 20)
        );
    }

    [TestMethod]
    public void TestEnabledItemIsWhiteAndItsShortcutCyan()
    {
        var row = Rows(Mixed, "Title")[0];

        Assert.AreEqual(Color.White, row.Fragments[0].Color);
        Assert.AreEqual(Color.Cyan, row.Fragments.First(f => f.Text == "a").Color);
    }

    // A disabled item is dark all the way, i.e. its shortcut too, which is how the menu shows that
    // pressing it would do nothing
    [TestMethod]
    public void TestDisabledItemAndItsShortcutAreDark()
    {
        var row = Rows(Mixed, "Title")[1];

        Assert.AreEqual(Color.Dark, row.Fragments[0].Color);
        Assert.AreEqual(Color.Dark, row.Fragments.First(f => f.Text == "bb").Color);
    }

    [TestMethod]
    public void TestSubMenuMarkerIsMagentaWhenEnabledAndDarkWhenDisabled()
    {
        var enabled = Rows([new SubMenu("Sub", "s", [Item("x")])], "Title")[0];
        var disabled = Rows([new SubMenu("Sub", "s", [Item("x")]) { IsDisabled = true }], "Title")[0];

        // A disabled sub menu is dark from its text to its marker, so the fragments are merged
        Assert.AreEqual(Color.BrightMagenta, MarkerColor(enabled));
        Assert.AreEqual(Color.Dark, MarkerColor(disabled));

        static Color MarkerColor(Text row) => row.Fragments.First(f => f.Text.Contains('>')).Color;
    }

    // The separator line fills the scrollbar column as well when no scrollbar is shown, and stops
    // one short of it when there is one, which is the whole point of that inverted looking test in
    // ToSeparatorText
    [TestMethod]
    public void TestSeparatorIsOneShorterWhenAScrollbarIsShown()
    {
        List<MenuItem> few = [Item("One"), new MenuSeparator()];
        List<MenuItem> many = [.. Enumerable.Range(0, 40).Select(i => Item($"Item {i}")), new MenuSeparator()];

        var withoutScrollbar = Rows(few, "T");
        var withScrollbar = Rows(many, "T");

        Assert.AreEqual(Width(few, "T") - 2 + 1, withoutScrollbar[1].ToString().Length);
        Assert.AreEqual(Width(many, "T") - 2, withScrollbar[40].ToString().Length);
    }

    // Pinned as current behavior: a sub menu row is two columns wider than the others, since the
    // columns reserved for the ' >' marker are written after the marker itself rather than instead
    // of it. Invisible today, the two extra columns being blank and clipped by the view.
    [TestMethod]
    public void TestSubMenuRowIsTwoColumnsWiderThanTheOthers()
    {
        var rows = Rows(Mixed, "Title");

        Assert.AreEqual(16, rows[0].ToString().Length); // a normal item
        Assert.AreEqual(18, rows[2].ToString().Length); // the sub menu item
    }

    static List<MenuItem> Mixed =>
        [
            Item("One", "a"),
            Item("Two", "bb") with
            {
                IsDisabled = true,
            },
            new SubMenu("Sub", "s", [Item("x")]),
            new MenuSeparator(),
            new MenuSeparator("Header"),
            Item("No shortcut"),
        ];

    static MenuItem Item(string text, string shortcut = "") => new MenuItem(text, shortcut, () => { });

    static MenuDimensions Dimensions(IReadOnlyList<MenuItem> items, string title, int screenWidth) =>
        MenuDimensions.Calculate(items, title, 5, 5, -1, screenWidth, 25);

    static int Width(IReadOnlyList<MenuItem> items, string title) => Dimensions(items, title, 80).Width;

    static IReadOnlyList<Text> Rows(IReadOnlyList<MenuItem> items, string title, int screenWidth = 80) =>
        MenuRows.ToRows(items, Dimensions(items, title, screenWidth));

    static string Picture(IReadOnlyList<MenuItem> items, string title, int screenWidth = 80) =>
        string.Join("\n", Rows(items, title, screenWidth).Select(r => $"|{r}|"));
}
