using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui.Common;

// The keys of an open menu: the key an item shows beside it picks that item, as Enter on it would.
// It used to do nothing, the menu knowing only the arrows, Enter and Escape. Which key picks which
// item is MenuShortcutsTest's; this is that pressing it in the running app does it.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class MenuTest
{
    // 'D' is shown beside 'Commit Diff' in the commit menu, and both cases open the diff, since
    // the menu writes it in upper case
    [TestMethod]
    [DataRow("d")]
    [DataRow("D")]
    public async Task TestTheKeyShownBesideAnItemRunsIt(string key)
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("m");
        gmd.WaitFor("Commit Diff");

        gmd.Send(key);

        gmd.WaitFor("Added: delta.txt");
        gmd.WaitUntilGone("Commit Diff");
    }

    // The key of a sub menu opens it, beside its item, as Enter or → does: 'S' in the diff menu
    // opens 'Scroll to', leaving the diff menu open behind it. 'Scroll to' is the first item, which
    // is the case that used to be swallowed: the menu took index 0 for the sub menu just closed.
    // Closing it with Escape then leaves Enter to open it again, which was swallowed the same way.
    [TestMethod]
    public async Task TestTheKeyOfASubMenuOpensIt()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("d");
        gmd.WaitFor("Added: delta.txt");
        gmd.Send("m");
        gmd.WaitFor("Diff Menu");

        gmd.Send("s");

        var screen = gmd.WaitFor("╭ Scroll to");
        StringAssert.Contains(screen, "Diff Menu", "The diff menu stays open behind its sub menu");

        gmd.Send("Escape");
        StringAssert.Contains(gmd.WaitUntilGone("╭ Scroll to"), "Diff Menu", "Escape closes the sub menu only");
        gmd.Send("Enter");
        gmd.WaitFor("╭ Scroll to");
    }

    // A greyed out item picked anyway, here by its key, says why on the status line at the bottom,
    // and the menu stays open. 'Diff Branch to' is greyed out while there are changes.
    [TestMethod]
    public async Task TestAGreyedOutItemSaysWhy()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("Left");
        gmd.WaitForStable();
        gmd.Send("m");
        gmd.WaitFor("Branch: main");

        gmd.Send("d");

        var screen = gmd.WaitFor("Commit or stash the changes first");
        Assert.AreEqual("Commit or stash the changes first", ScreenText.LastLine(screen));
        StringAssert.Contains(screen, "Branch: main", "The menu is still open");
    }
}
