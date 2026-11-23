using gmd.Cui.Common;
using Key = Terminal.Gui.Input.Key;

namespace gmdTest.Cui.Common;

// The keys that stand in for the arrow keys on a keyboard with none
[TestClass]
public class LetterArrowsTest
{
    [TestMethod]
    public void TestTheLettersAreTheArrows()
    {
        Assert.AreEqual(Key.CursorDown, LetterArrows.ArrowOf(Key.J));
        Assert.AreEqual(Key.CursorUp, LetterArrows.ArrowOf(Key.K));
        Assert.AreEqual(Key.CursorDown.WithShift, LetterArrows.ArrowOf((Key)'J'));
        Assert.AreEqual(Key.CursorUp.WithShift, LetterArrows.ArrowOf((Key)'K'));
        Assert.AreEqual(Key.CursorLeft, LetterArrows.ArrowOf((Key)','));
        Assert.AreEqual(Key.CursorRight, LetterArrows.ArrowOf((Key)'.'));
    }

    // h is not left, since it hides a branch in the log, and other keys are left as they are
    [TestMethod]
    public void TestOtherKeysAreThemselves()
    {
        Assert.AreEqual(Key.H, LetterArrows.ArrowOf(Key.H));
        Assert.AreEqual(Key.L, LetterArrows.ArrowOf(Key.L));
        Assert.AreEqual(Key.CursorDown, LetterArrows.ArrowOf(Key.CursorDown));
    }
}
