using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui;

// The start page, which gmd shows when it is started outside a repository, or with -m: the
// repositories to open, opened before or in the folder gmd was started in, and browsing to, cloning
// or creating one. It is all there is on screen until a repository is open, so it must never leave
// the user with nothing on screen.
//
// The repositories it lists are '.git' folders in a throwaway home, which is all the page looks at,
// so they read '~/...' on every machine. They fail to open, being no real repositories, which is
// what a test of a failed open wants; the tests that open one use an E2eRepo.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class MainViewTest
{
    // Someone who has not opened a repository yet is told what each action does, and the first one
    // is selected, so Enter does something. No search in the bar, there being nothing to search.
    [TestMethod]
    public void TestTheStartPageForSomeoneNew()
    {
        var home = Home();
        using var gmd = TmuxSession.StartGmd(home, In(home, "Downloads"));

        ScreenText.AssertEqual(
            """
             Gmd                                                                                                                ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

                                  ▸ o  Open a folder ...        browse to a repository on this computer
                                    c  Clone a repository ...   copy one from GitHub or another server
                                    i  Init a repository ...    start a new, empty one
                                    ?  Help                     how to read the log, in the browser
                                    a  About                    the version, and where to report a problem
                                    q  Quit
            """,
            gmd.WaitFor("Quit")
        );
    }

    // The recent repositories that are still repositories, most recent first, then the ones in the
    // folder gmd was started in that are not recent ones, numbered on from them, and no dot folder,
    // then the actions under a line
    [TestMethod]
    public void TestTheStartPageListsTheRepositories()
    {
        var home = Home("code/gmd", "code/web-shop", "code/api", "code/.hidden", "dotfiles");
        Directory.CreateDirectory(In(home, "code/notes"));
        home.SetRecentFolders([
            In(home, "code/web-shop"),
            In(home, "gone"),
            In(home, "code/notes"),
            In(home, "dotfiles"),
        ]);
        using var gmd = TmuxSession.StartGmd(home, In(home, "code"));

        ScreenText.AssertEqual(
            """
             Gmd                                                                                                                ? X
            ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

                                                         Recent repositories
                                                       ▸ 1  web-shop  ~/code/web-shop
                                                         2  dotfiles  ~/dotfiles

                                                         Repositories in ~/code
                                                         3  api       ~/code/api
                                                         4  gmd       ~/code/gmd

                                                         ────────────────────────────
                                                         o  Open a folder ...
                                                         c  Clone a repository ...
                                                         i  Init a repository ...
                                                         ?  Help
                                                         a  About
                                                         q  Quit
            """,
            gmd.WaitFor("Quit")
        );
    }

    // One key from the page to the log
    [TestMethod]
    public async Task TestANumberOpensThatRepository()
    {
        using var repo = await E2eRepo.CreateAsync();
        var home = Home("code/gmd");
        home.SetRecentFolders([In(home, "code/gmd"), repo.Path]);
        using var gmd = TmuxSession.StartGmd(home, home.Path);
        gmd.WaitFor("Quit");

        gmd.Send("2");

        gmd.WaitFor("Add delta");
        gmd.WaitUntilGone("Recent repositories");
    }

    [TestMethod]
    public async Task TestAClickOnARepositoryOpensIt()
    {
        using var repo = await E2eRepo.CreateAsync();
        var home = Home("code/gmd");
        home.SetRecentFolders([In(home, "code/gmd"), repo.Path]);
        using var gmd = TmuxSession.StartGmd(home, home.Path);
        var (x, y) = TmuxSession.PositionOf(gmd.WaitFor("Quit"), "2  ");

        gmd.Click(x + 4, y);

        gmd.WaitFor("Add delta");
    }

    // A repository that fails to open says so, and the page is back with it still selected, and its
    // keys work, as they did not when the start menu was a dialog that had closed
    [TestMethod]
    public async Task TestAFailedOpenStaysOnTheStartPage()
    {
        using var repo = await E2eRepo.CreateAsync();
        var home = Home("code/gmd");
        home.SetRecentFolders([In(home, "code/gmd"), repo.Path]);
        using var gmd = TmuxSession.StartGmd(home, home.Path);
        gmd.WaitFor("Quit");

        gmd.Send("Enter");
        gmd.WaitFor("Failed to load repo");
        gmd.Send("Enter");

        StringAssert.Contains(gmd.WaitUntilGone("Failed to load repo"), "▸ 1  gmd");
        gmd.Send("2");
        gmd.WaitFor("Add delta");
    }

    // Delete takes a repository off the list, and says that is all it did; Backspace too, which is
    // the delete key of a Mac. A repository in the start folder is no recent one to take off.
    [TestMethod]
    public void TestDeleteRemovesARecentRepository()
    {
        var home = Home("code/gmd", "code/web-shop", "dotfiles");
        home.SetRecentFolders([In(home, "code/gmd"), In(home, "code/web-shop")]);
        using var gmd = TmuxSession.StartGmd(home, home.Path);
        gmd.WaitFor("Quit");

        gmd.Send("DC");

        gmd.WaitUntilGone("1  gmd");
        // The message is drawn after the list, a moment later
        var screen = gmd.WaitFor("Removed ~/code/gmd");
        StringAssert.Contains(screen, "▸ 1  web-shop");
        Assert.AreEqual(
            "Removed ~/code/gmd from the recent list, the folder is untouched",
            ScreenText.LastLine(screen)
        );
        var config = File.ReadAllText(Path.Join(home.Path, ".gmdconfig"));
        Assert.IsFalse(config.Contains("code/gmd"), config);

        gmd.Send("BSpace");
        StringAssert.Contains(gmd.WaitUntilGone("1  web-shop"), "▸ 1  dotfiles");
        gmd.Send("DC");
        StringAssert.Contains(gmd.WaitFor("Only a recent repository"), "▸ 1  dotfiles");
    }

    [TestMethod]
    public void TestEscapeQuitsAtOnce()
    {
        var home = Home();
        using var gmd = TmuxSession.StartGmd(home, In(home, "Downloads"));
        gmd.WaitFor("Quit");

        gmd.Send("Escape");

        gmd.WaitForExit();
    }

    // Help copies the link, as ? does in the log (the session has no browser), and the page is still
    // there once the box with the link is closed
    [TestMethod]
    public void TestHelpStaysOnTheStartPage()
    {
        var home = Home();
        using var gmd = TmuxSession.StartGmd(home, In(home, "Downloads"));
        gmd.WaitFor("Quit");

        gmd.Send("?");

        gmd.WaitFor("copied to the clipboard");
        StringAssert.EndsWith(gmd.Clipboard(), "/gmd/doc/help.md");
        gmd.Send("Enter");
        StringAssert.Contains(gmd.WaitUntilGone("copied to the clipboard"), "▸ o  Open a folder");
    }

    // A clone or init that fails says so, and the page is there to go on from. Once, the start menu
    // was not shown again after the error, leaving a blank screen where no key but Escape did
    // anything.
    [TestMethod]
    public async Task TestAFailedInitStaysOnTheStartPage()
    {
        using var repo = await E2eRepo.CreateAsync();
        var home = Home();
        using var gmd = TmuxSession.StartGmd(home, In(home, "Downloads"));
        gmd.WaitFor("Quit");
        gmd.Send("i");
        gmd.WaitFor("Init Repo");
        ClearField(gmd);

        // A folder cannot be made inside a file
        gmd.SendText(Path.Join(repo.Path, "alpha.txt", "sub"));
        gmd.Send("Enter");
        gmd.WaitFor("Failed to init");
        gmd.Send("Enter");

        StringAssert.Contains(gmd.WaitUntilGone("Failed to init"), "▸ o  Open a folder");
    }

    [TestMethod]
    public async Task TestAFailedCloneStaysOnTheStartPage()
    {
        using var repo = await E2eRepo.CreateAsync();
        var home = Home();
        using var gmd = TmuxSession.StartGmd(home, In(home, "Downloads"));
        gmd.WaitFor("Quit");
        gmd.Send("c");
        gmd.WaitFor("Clone Repo");

        gmd.SendText(Path.Join(repo.Path, "no-such-repo.git"));
        gmd.Send("Tab");
        gmd.WaitForStable();
        gmd.SendText(Path.Join(repo.Path, "clone"));
        gmd.Send("Enter");
        gmd.WaitFor("Failed to clone");
        gmd.Send("Enter");

        StringAssert.Contains(gmd.WaitUntilGone("Failed to clone"), "▸ o  Open a folder");
    }

    // Init makes the repository in the folder gmd was started in, as the page says, and opens it
    [TestMethod]
    public void TestInitIsInTheStartFolder()
    {
        var home = Home();
        var downloads = In(home, "Downloads");
        using var gmd = TmuxSession.StartGmd(home, downloads);
        StringAssert.Contains(gmd.WaitFor("Quit"), "i  Init a repository ...    start a new, empty one");

        gmd.Send("i");
        gmd.WaitFor("Init Repo");
        gmd.Send("Enter");

        gmd.WaitUntilGone("Quit");
        Assert.IsTrue(Directory.Exists(Path.Join(downloads, ".git")), "Not made in the start folder");
    }

    // Started in the home folder, Init proposes a folder in it, to be named, and refuses the home
    // folder itself: Enter made the whole home folder a repo
    [TestMethod]
    public void TestInitInTheHomeFolderNamesAFolderInIt()
    {
        var home = Home();
        using var gmd = TmuxSession.StartGmd(home, home.Path);
        gmd.WaitFor("Quit");
        gmd.Send("i");
        gmd.WaitFor("Init Repo");

        gmd.Send("Enter");
        gmd.WaitFor("Name the folder to init");
        gmd.Send("Enter");
        gmd.WaitUntilGone("Name the folder to init");
        Assert.IsFalse(Directory.Exists(Path.Join(home.Path, ".git")), "Not the home folder");

        gmd.SendText("project");
        gmd.Send("Enter");

        gmd.WaitUntilGone("Quit");
        Assert.IsTrue(Directory.Exists(Path.Join(home.Path, "project", ".git")), "Made in the folder named");
    }

    // A clone is put in the folder gmd was started in, named as the repository is, and opened
    [TestMethod]
    public async Task TestACloneIsPutInTheStartFolder()
    {
        using var repo = await E2eRepo.CreateAsync();
        var home = Home();
        using var gmd = TmuxSession.StartGmd(home, In(home, "Downloads"));
        gmd.WaitFor("Quit");
        gmd.Send("c");
        gmd.WaitFor("Clone Repo");

        gmd.SendText(repo.Path);
        gmd.WaitForStable();
        gmd.Send("Enter");

        gmd.WaitFor("Add delta");
        Assert.IsTrue(Directory.Exists(Path.Join(In(home, "Downloads"), Path.GetFileName(repo.Path), ".git")));
    }

    // The keys of a dialog closed with Escape are back on the page, as they were not on the start
    // menu it replaced, which had closed for the dialog
    [TestMethod]
    public async Task TestTheKeysWorkAfterADialogIsCancelled()
    {
        using var repo = await E2eRepo.CreateAsync();
        var home = Home();
        home.SetRecentFolders([repo.Path]);
        using var gmd = TmuxSession.StartGmd(home, home.Path);
        gmd.WaitFor("Quit");
        gmd.Send("c");
        gmd.WaitFor("Clone Repo");

        gmd.Send("Escape");
        gmd.WaitUntilGone("Clone Repo");
        gmd.Send("1");

        gmd.WaitFor("Add delta");
    }

    // A repository that fails to open leaves the page with its own key hints, not the log's, which
    // would not work there. The repository is not listed, since only one that opened is.
    [TestMethod]
    public async Task TestARepoThatFailsToOpenShowsTheStartPageAndItsHints()
    {
        using var repo = await E2eRepo.CreateAsync();
        // A branch on a commit that is not there: git still finds the repository, but not its log
        repo.WriteFile(".git/refs/heads/broken", "0123456789012345678901234567890123456789\n");
        using var gmd = TmuxSession.StartGmd(repo, isKeyHints: true);
        gmd.WaitFor("Failed to load repo");
        gmd.Send("Enter");
        gmd.WaitUntilGone("Failed to load repo");

        // The page itself, rather than the screen once the box is gone: between the two the screen
        // can stand still with neither, for longer than WaitUntilGone waits, as on a busy CI runner
        // that compiled the page's code on its first show (2026-10-14)
        var screen = gmd.WaitFor("▸ o  Open a folder");
        StringAssert.StartsWith(ScreenText.LastLine(screen), "Enter open  o open folder  c clone  i init  Esc quit");
    }

    // A click beside the items does nothing, and certainly does not quit
    [TestMethod]
    public void TestAClickBesideTheItemsDoesNothing()
    {
        var home = Home();
        using var gmd = TmuxSession.StartGmd(home, In(home, "Downloads"));
        gmd.WaitFor("Quit");

        gmd.Click(110, 30);

        StringAssert.Contains(gmd.WaitForStable(), "▸ o  Open a folder");
        Assert.IsTrue(gmd.IsRunning, "A click beside the items should not quit gmd");
    }

    // -m shows the page inside a repository too, and Init proposes no folder inside it, nesting a
    // repository being seldom meant
    [TestMethod]
    public async Task TestTheStartPageInsideARepository()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo.Path, extraArgs: "-m");
        gmd.WaitFor("Quit");

        gmd.Send("i");

        StringAssert.Contains(gmd.WaitFor("Init Repo"), "│ Path:              ");
    }

    // On a small terminal the page leaves out its spacing and lists fewer repositories, rather than
    // putting the actions out of sight
    [TestMethod]
    public void TestASmallTerminalKeepsTheActionsInSight()
    {
        var recent = Enumerable.Range(1, 12).Select(i => $"code/recent{i:00}").ToArray();
        var inFolder = Enumerable.Range(1, 5).Select(i => $"code/other{i}").ToArray();
        var home = Home([.. recent, .. inFolder]);
        home.SetRecentFolders(recent.Select(r => In(home, r)).ToList());
        using var gmd = TmuxSession.StartGmd(home, In(home, "code"), width: 80, height: 24);

        var screen = gmd.WaitFor("Quit");

        StringAssert.Contains(screen, "▸ 1  recent01");
        StringAssert.Contains(screen, "… and 9 more");
        StringAssert.Contains(screen, "q  Quit");
    }

    // A home with the repositories given, as '.git' folders, and a 'Downloads' folder with none
    static TempHome Home(params string[] repos)
    {
        var home = TempHome.Create();
        Directory.CreateDirectory(In(home, "Downloads"));
        foreach (var repo in repos)
            Directory.CreateDirectory(Path.Join(In(home, repo), ".git"));
        return home;
    }

    static string In(TempHome home, string path) => Path.Join(home.Path, path);

    // Empties the text field with the focus: to its start, then cut to its end
    static void ClearField(TmuxSession gmd)
    {
        gmd.Send("Home");
        gmd.WaitForStable();
        gmd.Send("C-k");
        gmd.WaitForStable();
    }
}
