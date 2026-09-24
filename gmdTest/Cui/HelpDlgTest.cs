using gmd.Cui;

namespace gmdTest.Cui;

// The help page is drawn as it is written, one row per line and no wrapping, in a dialog of fixed
// width, so a line wider than the dialog is cut off and the end of its sentence is simply lost.
// Measured on the rows as drawn, since the markup (`code` and *emphasis*) is dropped then.
[TestClass]
public class HelpDlgTest
{
    [TestMethod]
    public void TestEveryHelpRowFitsTheDialog()
    {
        var content = AssertOk(Files.GetEmbeddedFileContentText(HelpDlg.HelpFile));

        var tooWide = HelpDlg
            .ToHelpText(content)
            .Select((row, i) => (Line: i + 1, Text: row.ToString().TrimEnd()))
            .Where(r => r.Text.Length > HelpDlg.TextWidth)
            .Select(r => $"help.md:{r.Line} is {r.Text.Length} wide: {r.Text}")
            .ToList();

        Assert.AreEqual(0, tooWide.Count, $"Wider than {HelpDlg.TextWidth}:\n{string.Join("\n", tooWide)}");
    }

    [TestMethod]
    public void TestMarkupIsNotCountedInTheWidth()
    {
        var rows = HelpDlg.ToHelpText("Press `Enter` on **Commit ...** for *more*");

        Assert.AreEqual("Press Enter on Commit ... for more", rows.Single().ToString());
    }
}
