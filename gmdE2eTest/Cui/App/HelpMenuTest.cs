using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui.App;

// The Help menu in the repo menu, whose bug report opens GitHub's issue form with the version, the
// system and git filled in. The test's BROWSER is empty, so the link is copied rather than opened,
// which is how it is read back.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class HelpMenuTest
{
    [TestMethod]
    public async Task TestReportABugCopiesTheFilledInForm()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        OpenHelpMenu(gmd);
        gmd.SelectMenuItem("Report a Bug");
        gmd.Send("Enter");

        var screen = gmd.WaitFor("is copied to the clipboard.");
        StringAssert.Contains(screen, "the link to a bug report");
        StringAssert.Contains(screen, "Add the log to it, ~/gmd.log.");

        var link = gmd.Clipboard();
        StringAssert.StartsWith(link, "https://github.com/michael-reichenauer/gmd/issues/new?template=bug_report.yml");
        StringAssert.Contains(link, "&version=");
        StringAssert.Contains(link, "%2C%20a%20dev%20build"); // ', a dev build'
        StringAssert.Contains(link, "TERM%3D"); // tmux sets TERM
        StringAssert.Contains(link, "%2C%20in%20tmux%2C%20colors%2016&"); // the palette the tests draw in
        StringAssert.Contains(link, "&git=2.");
    }

    [TestMethod]
    public async Task TestSuggestAnImprovementCopiesTheFormWithTheVersion()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        OpenHelpMenu(gmd);
        gmd.SelectMenuItem("Suggest an Improvement");
        gmd.Send("Enter");

        gmd.WaitFor("the link to a suggestion");
        var link = gmd.Clipboard();
        StringAssert.StartsWith(link, "https://github.com/michael-reichenauer/gmd/issues/new?template=suggestion.yml");
        StringAssert.Contains(link, "&version=");
        Assert.IsFalse(link.Contains("&system="), "Only the version");
    }

    // The Help submenu, at the bottom of the repo menu, open with its items showing
    internal static void OpenHelpMenu(TmuxSession gmd)
    {
        gmd.Send("M");
        gmd.WaitFor("Repo Menu");
        gmd.SelectMenuItem("Help");
        gmd.Send("Enter");
        gmd.WaitFor("Report a Bug");
    }
}
