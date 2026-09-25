using gmd.Cui.Common;
using Key = Terminal.Gui.Key;

namespace gmdTest.Cui.Common;

// The keys that pick an item of an open menu, i.e. what the shortcut column of a menu is read as
[TestClass]
public class MenuShortcutsTest
{
    // A letter is written in upper case and pressed in lower case; "Shift-" is the upper case one
    [TestMethod]
    public void TestLettersAndShiftLetters()
    {
        CollectionAssert.AreEqual(new[] { Key.c }, Keys("C"));
        CollectionAssert.AreEqual(new[] { Key.E }, Keys("Shift-E"));
    }

    [TestMethod]
    public void TestSeveralKeysAndOtherKeys()
    {
        CollectionAssert.AreEqual(new[] { (Key)']', Key.n }, Keys("], N"));
        CollectionAssert.AreEqual(new[] { (Key)'?', Key.F1 }, Keys("?, F1"));
        CollectionAssert.AreEqual(new[] { Key.D1 }, Keys("1"));
        CollectionAssert.AreEqual(new[] { (Key)'+' }, Keys("+"));
        CollectionAssert.AreEqual(new[] { Key.C | Key.CtrlMask }, Keys("Ctrl-C"));
        CollectionAssert.AreEqual(new[] { Key.W | Key.AltMask }, Keys("Alt-W"));
        CollectionAssert.AreEqual(new[] { Key.F7 }, Keys("F7"));
        CollectionAssert.AreEqual(new[] { Key.Backspace }, Keys("Backspace"));
    }

    // What the menu itself uses, and what the column holds that is not a key, gives no key
    [TestMethod]
    public void TestWhatIsNotAKeyOfTheMenuGivesNone()
    {
        CollectionAssert.AreEqual(new[] { Key.q }, Keys("Q, Esc"), "Esc closes the menu");
        foreach (var shortcut in new[] { "", "Enter", "Esc", "Esc ", "←", "→", "Shift →", "'M R'", "(no suggestions)" })
            Assert.AreEqual(0, Keys(shortcut).Length, $"'{shortcut}' should give no key");
    }

    // Both cases of a letter pick the item showing it, as both are pressed
    [TestMethod]
    public void TestALetterPicksItsItemInBothCases()
    {
        var keys = MenuShortcuts.Of([Item("Commit ...", "C"), Item("Amend ...", "A")]);

        Assert.AreEqual(0, keys[Key.c]);
        Assert.AreEqual(0, keys[Key.C]);
        Assert.AreEqual(1, keys[Key.a]);
    }

    // ... unless the upper case is another item's "Shift-", which is a different command: in a
    // branch menu 'p' pushes the branch and 'P' pushes every branch, wherever they are in the menu
    [TestMethod]
    public void TestAShiftLetterIsItsOwnItem()
    {
        var keys = MenuShortcuts.Of([Item("Push All Branches", "Shift-P"), Item("Push", "P")]);

        Assert.AreEqual(1, keys[Key.p]);
        Assert.AreEqual(0, keys[Key.P]);
    }

    // A greyed out item picks nothing, and still keeps its key from the item it would otherwise fall
    // back to: with 'Push All Branches' disabled, 'P' must not push this branch instead
    [TestMethod]
    public void TestADisabledItemKeepsItsKeyAndPicksNothing()
    {
        var keys = MenuShortcuts.Of([
            Item("Push", "P"),
            Item("Push All Branches", "Shift-P") with
            {
                IsDisabled = true,
            },
        ]);

        Assert.AreEqual(0, keys[Key.p]);
        Assert.IsFalse(keys.ContainsKey(Key.P));
    }

    // The first item showing a key wins
    [TestMethod]
    public void TestTheFirstItemWithAKeyPicksIt()
    {
        var keys = MenuShortcuts.Of([Item("First", "D"), Item("Second", "D")]);

        Assert.AreEqual(0, keys[Key.d]);
    }

    static Key[] Keys(string shortcut) => MenuShortcuts.KeysOf(shortcut).ToArray();

    static MenuItem Item(string text, string shortcut) => new(text, shortcut, () => { });
}
