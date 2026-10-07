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

    // The diff, blame and conflict views open the help at the part about them, by its heading, so
    // a heading renamed in help.md must be renamed in HelpDlg too, or the help opens at the top
    [TestMethod]
    public void TestTheSectionsTheViewsOpenAtAreInTheHelp()
    {
        var content = AssertOk(Files.GetEmbeddedFileContentText(HelpDlg.HelpFile));

        var names = HelpDlg.Sections(content).Select(s => s.Name).ToList();

        CollectionAssert.Contains(names, HelpDlg.DiffSection);
        CollectionAssert.Contains(names, HelpDlg.ConflictSection);
    }

    // ']' goes to the next heading below the top of the view and '[' to the one above, or to the top
    // of the help before the first one
    [TestMethod]
    public void TestTheSectionsAreSteppedThrough()
    {
        var sections = HelpDlg.Sections("# Title\n\n## One\ntext\n## Two\ntext\n");

        CollectionAssert.AreEqual(new[] { ("One", 2), ("Two", 4) }, sections.ToArray());
        Assert.AreEqual(2, HelpDlg.SectionIndexFrom(sections, 0, 1));
        Assert.AreEqual(4, HelpDlg.SectionIndexFrom(sections, 2, 1));
        Assert.AreEqual(-1, HelpDlg.SectionIndexFrom(sections, 4, 1), "None after the last");
        Assert.AreEqual(2, HelpDlg.SectionIndexFrom(sections, 4, -1));
        Assert.AreEqual(2, HelpDlg.SectionIndexFrom(sections, 3, -1), "Into a section, back to its heading");
        Assert.AreEqual(0, HelpDlg.SectionIndexFrom(sections, 2, -1), "Before the first is the top");
    }
}
