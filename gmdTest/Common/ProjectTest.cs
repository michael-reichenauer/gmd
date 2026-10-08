using gmd.Common;

namespace gmdTest.Common;

// The help is gmd/doc/help.md on GitHub, linked at a heading of it by the id GitHub gives that
// heading, so a link made wrong opens the help at the top rather than failing
[TestClass]
public class ProjectTest
{
    const string Sha = "0123456789abcdef0123456789abcdef01234567";

    [TestMethod]
    public void TestHelpUrlIsAtTheCommitBuilt()
    {
        Assert.AreEqual(
            $"https://github.com/michael-reichenauer/gmd/blob/{Sha}/gmd/doc/help.md",
            Project.HelpUrl("", Sha)
        );
    }

    [TestMethod]
    public void TestHelpUrlIsOnMainForABuildNotCis()
    {
        Assert.AreEqual(
            "https://github.com/michael-reichenauer/gmd/blob/main/gmd/doc/help.md",
            Project.HelpUrl("", "")
        );
    }

    [TestMethod]
    public void TestHelpUrlIsAtTheSection()
    {
        Assert.AreEqual(
            "https://github.com/michael-reichenauer/gmd/blob/main/gmd/doc/help.md#diff-and-blame",
            Project.HelpUrl("Diff and Blame", "")
        );
    }

    [TestMethod]
    public void TestAnchorIsTheIdGitHubGivesTheHeading()
    {
        Assert.AreEqual("resolving-conflicts", Project.Anchor("Resolving Conflicts"));
        Assert.AreEqual("how-gmd-picks-a-commits-branch", Project.Anchor("How Gmd Picks a Commit's Branch"));
        Assert.AreEqual("continue-skip-or-abort", Project.Anchor("Continue, Skip or Abort"));
        Assert.AreEqual("when-origin-was-force-pushed", Project.Anchor("When Origin Was Force Pushed"));
        Assert.AreEqual("a-b_c-d", Project.Anchor("A-b_c (d)"), "Hyphens and underscores are kept");
    }
}
