using gmd.Common;
using gmd.Cui;

namespace gmdTest.Cui;

// The help is gmd/doc/help.md on GitHub, which gmd opens at a heading, so a heading renamed in
// help.md must be renamed where it is linked to as well, or the help opens at the top
[TestClass]
public class HelpPageTest
{
    // help.md, copied beside the tests (gmdTest.csproj), since gmd links to it rather than embeds it
    static readonly string Help = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "help.md"));

    // The ids GitHub gives the headings, '#', '##' and '###' alike
    static List<string> Anchors =>
        Help.Split('\n').Where(l => l.StartsWith('#')).Select(l => Project.Anchor(l.TrimStart('#'))).ToList();

    [TestMethod]
    public void TestTheSectionsTheViewsOpenAtAreInTheHelp()
    {
        CollectionAssert.Contains(Anchors, Project.Anchor(HelpPage.DiffSection));
        CollectionAssert.Contains(Anchors, Project.Anchor(HelpPage.ConflictSection));
    }
}
