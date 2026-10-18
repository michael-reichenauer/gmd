using gmd.Cui.Common;
using Terminal.Gui.Drivers;
using TgColor = Terminal.Gui.Drawing.Color;

namespace gmdTest.Cui.Common;

// Which palette gmd draws in, by the Colors setting, what the terminal can do and its background
[TestClass]
public class PaletteTest
{
    static readonly TgColor White = new(255, 255, 255);
    static readonly TgColor Black = new(12, 12, 12);

    // Auto is the light palette on a light background, the dark one on a dark background and on a
    // terminal that has not said, or does not, what its background is
    [TestMethod]
    public void TestAutoFollowsTheTerminalsBackground()
    {
        Assert.AreSame(Palette.Light, Palette.Choose("auto", ColorCapabilityLevel.TrueColor, White));
        Assert.AreSame(Palette.Dark, Palette.Choose("auto", ColorCapabilityLevel.TrueColor, Black));
        Assert.AreSame(Palette.Dark, Palette.Choose("auto", ColorCapabilityLevel.TrueColor, null));
    }

    [TestMethod]
    public void TestDarkAndLightAreWhatTheySay()
    {
        Assert.AreSame(Palette.Dark, Palette.Choose("dark", ColorCapabilityLevel.TrueColor, White));
        Assert.AreSame(Palette.Light, Palette.Choose("light", ColorCapabilityLevel.TrueColor, Black));
    }

    // The 16 colors when asked for, and whatever is asked for on a terminal of 16 colors or none
    [TestMethod]
    public void TestSixteenColorsWhenAskedOrAllThereIs()
    {
        Assert.AreSame(Palette.Sixteen, Palette.Choose("16", ColorCapabilityLevel.TrueColor, White));
        Assert.AreSame(Palette.Sixteen, Palette.Choose("light", ColorCapabilityLevel.Colors16, White));
        Assert.AreSame(Palette.Sixteen, Palette.Choose("auto", ColorCapabilityLevel.NoColor, null));
    }

    // The 16 colors keep the five branch colors gmd has always had, by their own names, so the
    // graph looks as it did; the others have twelve
    [TestMethod]
    public void TestTheBranchColorsOfEachPalette()
    {
        CollectionAssert.AreEqual(
            new[] { Color.Blue, Color.Green, Color.Cyan, Color.Red, Color.Yellow },
            Palette.Sixteen.BranchColors.ToArray()
        );
        Assert.AreEqual(12, Palette.Dark.BranchColors.Count);
        Assert.AreEqual(12, Palette.Light.BranchColors.Count);
    }
}
