using gmd.Common.Spelling;
using gmd.Cui.Common;
using gmdTest.Fixtures;
using Terminal.Gui;
using MenuItem = gmd.Cui.Common.MenuItem;

namespace gmdTest.Cui.Common;

[TestClass]
public class TextContextMenuTest
{
    // The misspelled word of "Sumerize the brnach" the caret is on
    static readonly WordSpan Brnach = new(13, 6, "brnach");

    static FakeSpellChecker Checker() => new(["Sumerize", "brnach"], new() { ["brnach"] = ["branch", "breach"] });

    static IEnumerable<MenuItem> SpellingItems(ISpellChecker? checker, WordSpan? at, bool hasMisspelled) =>
        TextContextMenu.SpellingItems(checker, at, hasMisspelled, _ => { }, () => { }, () => { });

    [TestMethod]
    public void TestIsMenuKey()
    {
        Assert.IsTrue(TextContextMenu.IsMenuKey(Key.F10 | Key.ShiftMask));
        Assert.IsFalse(TextContextMenu.IsMenuKey(Key.F10));
        Assert.IsFalse(TextContextMenu.IsMenuKey(Key.F7));
    }

    [TestMethod]
    public void TestOnAMisspelledWordTheSuggestionsComeFirst()
    {
        string? replaced = null;
        var items = TextContextMenu
            .SpellingItems(Checker(), Brnach, true, with => replaced = with, () => { }, () => { })
            .ToList();

        CollectionAssert.AreEqual(
            new[] { "branch", "breach", "Add 'brnach' to dictionary" },
            items.Select(i => i.Text).ToList()
        );
        Assert.AreEqual("Spelling", TextContextMenu.Title(Brnach));

        items[0].Action();
        Assert.AreEqual("branch", replaced);
    }

    [TestMethod]
    public void TestOffAWordTheMenuIsTheWayToTheSuggestionsWithItsKey()
    {
        bool shown = false;
        var item = TextContextMenu
            .SpellingItems(Checker(), null, true, _ => { }, () => shown = true, () => { })
            .Single();

        Assert.AreEqual("Spelling Suggestions ...", item.Text);
        Assert.AreEqual("F7", item.Shortcut);
        Assert.IsTrue(item.CanExecute!());
        Assert.AreEqual("Edit", TextContextMenu.Title(null));

        item.Action();
        Assert.IsTrue(shown);

        // With nothing misspelled the entry stays, disabled, so the key is still seen
        var none = SpellingItems(Checker(), null, false).Single();
        Assert.IsFalse(none.CanExecute!());
    }

    [TestMethod]
    public void TestNoSpellingPartWhenSpellCheckingIsOff()
    {
        Assert.AreEqual(0, SpellingItems(null, null, true).Count());

        var off = Checker();
        off.IsEnabled = false;
        Assert.AreEqual(0, SpellingItems(off, Brnach, true).Count());
    }

    [TestMethod]
    public void TestTheEditActionsFollowTheSpellingWithTheKeysOfTheView()
    {
        var view = new UITextView { Text = "Sumerize the brnach" };
        var items = TextContextMenu.Items(SpellingItems(Checker(), Brnach, true), view, () => false).ToList();

        CollectionAssert.AreEqual(
            new[]
            {
                "branch",
                "breach",
                "Add 'brnach' to dictionary",
                "",
                "Select All",
                "Copy",
                "Cut",
                "Paste",
                "Undo",
                "Redo",
            },
            items.Select(i => i.Text).ToList()
        );
        Assert.IsInstanceOfType<MenuSeparator>(items[3]);
        CollectionAssert.AreEqual(
            new[] { "Ctrl-T", "Alt-C", "Alt-W", "Ctrl-Y", "Ctrl-Z", "Ctrl-R" },
            items.Skip(4).Select(i => i.Shortcut).ToList()
        );
        Assert.IsFalse(items[5].CanExecute!(), "Copy needs a selection");
        Assert.IsFalse(items[6].CanExecute!(), "Cut needs a selection");
        Assert.IsNull(items[7].CanExecute, "Paste is always offered");
    }

    [TestMethod]
    public void TestWithNoSpellingPartThereIsNoSeparatorEither()
    {
        var field = new UITextField(0, 0, 30, "Fix resonable issue");
        var items = TextContextMenu.Items([], field, () => true).ToList();

        CollectionAssert.AreEqual(
            new[] { "Select All", "Copy", "Cut", "Paste", "Undo", "Redo" },
            items.Select(i => i.Text).ToList()
        );
        // A TextField binds other keys than a TextView does
        CollectionAssert.AreEqual(
            new[] { "Ctrl-T", "Ctrl-C", "Ctrl-X", "Ctrl-V", "Ctrl-Z", "Ctrl-Y" },
            items.Select(i => i.Shortcut).ToList()
        );
        Assert.IsTrue(items[1].CanExecute!());
    }

    // The edit actions are not public in Terminal.Gui, so an item runs its action by the key the
    // view binds it to
    [TestMethod]
    public void TestAnEditActionRunsByItsKey()
    {
        var view = new UITextView { Text = "Sumerize the brnach" };
        var items = TextContextMenu.Items([], view, () => false).ToList();
        Assert.AreEqual(0, view.SelectedLength);

        items.Single(i => i.Text == "Select All").Action();

        Assert.AreEqual("Sumerize the brnach", view.SelectedText.ToString());
    }
}
