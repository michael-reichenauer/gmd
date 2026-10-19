using gmd.Cui.Common;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;

namespace gmdTest.Cui.Common;

// Which way the wheel was turned, read from what a terminal sends, as Terminal.Gui parses it: the
// button 64 for up, 65 down, 66 left and 67 right, with 4 added for Shift and 16 for Ctrl
[TestClass]
public class WheelTest
{
    [TestMethod]
    [DataRow(64, -1, 0, "Up")]
    [DataRow(65, 1, 0, "Down")]
    [DataRow(66, 0, -1, "Left")]
    [DataRow(67, 0, 1, "Right")]
    [DataRow(68, 0, -1, "Up with Shift")]
    [DataRow(69, 0, 1, "Down with Shift")]
    [DataRow(80, 0, -1, "Up with Ctrl")]
    [DataRow(81, 0, 1, "Down with Ctrl")]
    [DataRow(0, 0, 0, "The left button")]
    public void TestWheelAsTheTerminalSendsIt(int button, int vertical, int sideways, string turn)
    {
        var flags = new AnsiMouseParser().ProcessMouseInput($"\u001b[<{button};10;5M")!.Flags;

        Assert.AreEqual(vertical, Wheel.Vertical(flags), turn);
        Assert.AreEqual(sideways, Wheel.Sideways(flags), turn);
    }

    // Why Wheel is there: a test for the vertical wheel is true for the sideways one too
    [TestMethod]
    public void TestSidewaysWheelHasTheFlagsOfTheVerticalWithCtrl()
    {
        Assert.AreEqual(MouseFlags.Ctrl | MouseFlags.WheeledUp, MouseFlags.WheeledLeft);
        Assert.AreEqual(MouseFlags.Ctrl | MouseFlags.WheeledDown, MouseFlags.WheeledRight);
    }
}
