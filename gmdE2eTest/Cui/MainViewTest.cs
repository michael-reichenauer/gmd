using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui;

// The start menu, which gmd shows when it is started outside a repository, or with -m: the recent
// repositories, and browsing to, cloning or creating one. It is all there is on screen until a
// repository is open, so it must never leave the user with nothing on screen.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class MainViewTest
{
    // Titled with what it is for, and saying there are no recent repositories rather than opening on
    // a bare separator, as it did for someone who had not opened one yet
    [TestMethod]
    public async Task TestTheStartMenuSaysWhatItIsFor()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = StartOnTheStartMenu(repo);

        ScreenText.AssertEqual(
            """
             Gmd                                                                                                     [Ϙ Search] ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
                ╭ Open a Repository ────────╮
                │No recent repositories     │
                │───────────────────────────│
                │Browse ...                 │
                │Clone ...                  │
                │Init ...                   │
                │Help ...                   │
                │About ...                  │
                │Quit                   Esc │
                ╰───────────────────────────╯
            """,
            gmd.WaitForStable(),
            repo.Path
        );
    }

    // A clone or init that fails says so and then shows the start menu again. It used to return
    // after the error, leaving a blank screen where no key but Escape did anything, and nothing
    // on it said so.
    [TestMethod]
    public async Task TestAFailedInitGoesBackToTheStartMenu()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = StartOnTheStartMenu(repo);
        SelectItem(gmd, "Init ...");
        gmd.WaitFor("Init Repo");

        // A folder cannot be made inside a file
        gmd.SendText(Path.Join(repo.Path, "alpha.txt", "sub"));
        gmd.Send("Enter");
        gmd.WaitFor("Failed to init");
        gmd.Send("Enter");

        gmd.WaitFor("Open a Repository");
    }

    [TestMethod]
    public async Task TestAFailedCloneGoesBackToTheStartMenu()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = StartOnTheStartMenu(repo);
        SelectItem(gmd, "Clone ...");
        gmd.WaitFor("Clone Repo");

        gmd.SendText(Path.Join(repo.Path, "no-such-repo.git"));
        gmd.Send("Tab");
        gmd.WaitForStable();
        gmd.SendText(Path.Join(repo.Path, "clone"));
        gmd.Send("Enter");
        gmd.WaitFor("Failed to clone");
        gmd.Send("Enter");

        gmd.WaitFor("Open a Repository");
    }

    // A click beside the start menu leaves it open. It used to quit gmd, the way Escape does, which
    // the menu says as 'Quit Esc'; a click is not asking for that.
    [TestMethod]
    public async Task TestAClickOutsideTheStartMenuDoesNotQuit()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = StartOnTheStartMenu(repo);

        gmd.Click(80, 30);

        StringAssert.Contains(gmd.WaitForStable(), "Open a Repository", "The menu is still open");
        Assert.IsTrue(gmd.IsRunning, "A click outside the menu should not quit gmd");
    }

    // -m shows the start menu even inside a repository. The home is new, so there are no recent
    // repositories, and the menu is the same few items every time.
    static TmuxSession StartOnTheStartMenu(TempRepo repo)
    {
        var gmd = TmuxSession.StartGmd(repo.Path, extraArgs: "-m");
        gmd.WaitFor("Open a Repository");
        return gmd;
    }

    // Counted up from the last item, 'Quit', which does not move whatever comes above it
    static void SelectItem(TmuxSession gmd, string item)
    {
        string[] fromTheEnd = ["Quit", "About ...", "Help ...", "Init ...", "Clone ...", "Browse ..."];
        gmd.Send("End");
        gmd.WaitForStable();
        for (int i = 0; i < Array.IndexOf(fromTheEnd, item); i++)
        {
            gmd.Send("Up");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");
    }
}
