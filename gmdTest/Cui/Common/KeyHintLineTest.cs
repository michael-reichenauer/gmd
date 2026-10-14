using gmd.Cui.Common;

namespace gmdTest.Cui.Common;

// The key-hint line: the border, the hints from the left, the border on to the help at the right, and
// what does not fit dropped from the end rather than cut, so every hint shown is whole; and a status
// message in its place
[TestClass]
public class KeyHintLineTest
{
    [TestMethod]
    public void TestTheLineDropsTheHintsThatDoNotFit()
    {
        List<KeyHint> hints = [new("m", "menu"), new("d", "diff"), new("f", "search")];

        Assert.AreEqual("────────── m menu  d diff  f search ──── ? help ──", KeyHintLine.ToText(hints, 50).ToString());
        Assert.AreEqual("────────── m menu  d diff ── ? help ──", KeyHintLine.ToText(hints, 38).ToString());
        Assert.AreEqual("────────────── ? help ──", KeyHintLine.ToText(hints, 24).ToString());
        Assert.AreEqual(
            "────────── ? help ──",
            KeyHintLine.ToText(hints, 20).ToString(),
            "The help is kept to the last"
        );
    }

    // Set into a border in the color of the line under the application bar, so that it reads as the
    // frame of the log rather than as one more row of it: the keys in cyan, what they do in dark gray,
    // like the shortcuts in a menu, and the border in bright magenta
    [TestMethod]
    public void TestTheLineIsABorderWithTheKeysInCyan()
    {
        var text = KeyHintLine.ToText([new("d", "diff")], 34);

        Assert.AreEqual("────────── d diff ────── ? help ──", text.ToString());
        Assert.AreEqual("MMMMMMMMMMDCDDDDDDMMMMMMDCDDDDDDMM", Colors(text));
    }

    // A status message takes the line, set into the border the same way: green for what was done,
    // yellow for why nothing was, red for a failure, cyan for what is being done, and cut with '┅'
    // when it is longer than the line
    [TestMethod]
    public void TestAStatusMessageIsDrawnInTheColorOfItsKind()
    {
        var info = KeyHintLine.ToText(new StatusMessage("Pushed 'main'", StatusKind.Info, DateTime.UtcNow), 28);
        var notice = KeyHintLine.ToText(new StatusMessage("Nothing to commit", StatusKind.Notice, DateTime.UtcNow), 32);
        var failure = KeyHintLine.ToText(new StatusMessage("Fetch failed", StatusKind.Failure, DateTime.UtcNow), 28);
        var progress = KeyHintLine.ToText(
            new StatusMessage("Pushing 'main'...", StatusKind.Progress, DateTime.UtcNow),
            32
        );

        Assert.AreEqual("────────── Pushed 'main' ───", info.ToString());
        Assert.AreEqual(Color.Green, ColorOf(info, "Pushed"));
        Assert.AreEqual(Color.Yellow, ColorOf(notice, "Nothing"));
        Assert.AreEqual(Color.BrightRed, ColorOf(failure, "Fetch"));
        Assert.AreEqual(Color.Cyan, ColorOf(progress, "Pushing"));
    }

    [TestMethod]
    public void TestALongStatusMessageIsCut()
    {
        var text = KeyHintLine.ToText(
            new StatusMessage("Nothing to push on 'feature/login'", StatusKind.Notice, DateTime.UtcNow),
            28
        );

        Assert.AreEqual("────────── Nothing to pu┅ ──", text.ToString());
    }

    // One letter per cell: 'M' the magenta border, 'C' a cyan key, 'D' the dark rest
    static string Colors(Text text) =>
        string.Concat(
            text.Fragments.Select(f => new string(
                f.Color == Color.BrightMagenta ? 'M'
                    : f.Color == Color.Cyan ? 'C'
                    : 'D',
                f.Text.Length
            ))
        );

    static Color ColorOf(Text text, string part) => text.Fragments.First(f => f.Text.Contains(part)).Color;
}
