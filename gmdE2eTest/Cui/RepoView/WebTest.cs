using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui.RepoView;

// Opening what the log shows on the web site hosting the remote. The session has no browser, as
// over ssh (TmuxSession empties BROWSER and DISPLAY), so gmd copies the link instead, and that is
// what is asserted: the page it would have opened.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class WebTest
{
    [TestMethod]
    public async Task TestOpenCommitInBrowserCopiesTheLinkWithNoBrowser()
    {
        using var repo = await E2eRepo.CreateWithOriginAsync();
        var delta = (await repo.GitAsync("rev-parse HEAD~1")).Trim();
        // The remote as a GitHub one, which never reaches the network: https is not allowed, so the
        // fetch gmd runs fails at once rather than going out to github.com
        await repo.GitAsync("remote set-url origin https://github.com/user/repo.git");
        await repo.GitAsync("config protocol.https.allow never");
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Add delta");

        // 'Add delta', the last commit pushed, and its menu, where the item is four up from the end,
        // past 'Branches', the separator and the two file items
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("m");
        gmd.WaitFor("Commit Diff");
        gmd.Send("End");
        gmd.WaitForStable();
        for (int i = 0; i < 4; i++)
        {
            gmd.Send("Up");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");

        Assert.AreEqual(
            "There is no browser to open it in here: copied the link to the clipboard",
            ScreenText.LastLine(gmd.WaitFor("copied the link"))
        );
        Assert.AreEqual($"https://github.com/user/repo/commit/{delta}", gmd.Clipboard());
    }
}
