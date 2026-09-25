using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui;

// The About dialog, from the repo menu
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class AboutDlgTest
{
    // With update checks off, as in every end-to-end test, there is no latest version known, which
    // the dialog has to take in its stride, and what it says is text rather than a type name
    [TestMethod]
    public async Task TestAboutSaysTheVersions()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Initial");
        gmd.Send("M");
        gmd.WaitFor("Repo Menu");
        gmd.Send("End");
        gmd.WaitForStable();
        gmd.Send("Up");
        gmd.WaitForStable();

        gmd.Send("Enter");

        var screen = gmd.WaitFor("Version:");
        StringAssert.Contains(screen, "Updates: ");
        StringAssert.Contains(screen, "Git:     ");
        Assert.IsFalse(screen.Contains("System."), "No type name in place of a version");
    }
}
