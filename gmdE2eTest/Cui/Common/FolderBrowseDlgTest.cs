using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui.Common;

// The folder browser, which a repository is opened from (the start page's Open a folder and the repo
// menu's Browse) and Clone, Init and Create Worktree pick their folder in: the tree of folders from
// the most likely one, open, the arrow keys opening and closing its folders, and typing to find a
// folder or browse a path instead. What the tree and the finder list, and in what order, is
// FolderTreeTest's and FolderFinderTest's.
//
// The folders are in a throwaway home, so they read '~/...' on every machine, and the repositories
// in it are '.git' folders, which is all the browser looks at.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class FolderBrowseDlgTest
{
    // Started in ~/code, the browser opens on it, open, the repositories in it marked, then home and
    // the root, closed; the status line says where the cursor is and what Enter does. → and ↓ go
    // into a folder, and ← back up.
    [TestMethod]
    public void TestTheBrowserOpensOnTheFolderStartedIn()
    {
        var home = Home("code/gmd", "code/web-shop");
        Directory.CreateDirectory(In(home, "code/notes/drafts"));
        using var gmd = TmuxSession.StartGmd(home, In(home, "code"));
        gmd.WaitFor("Quit");

        gmd.Send("o");
        var screen = gmd.WaitFor("~/code   ↑↓ select  ←→ folders  Enter open");
        Assert.AreEqual(
            """
                           ╭ Open Repo ─────────────────────────────────────────────────────────────────────────────╮
                           │ Find:                                                                                ] │
                           │────────────────────────────────────────────────────────────────────────────────────────│
                           │ ▾ ~/code                                                                               │
                           │ │ ▸ gmd                                                                          repo  │
                           │ │ ▸ notes                                                                              │
                           │ │ ▸ web-shop                                                                     repo  │
                           │ ▸ ~                                                                                    │
                           │ ▸ /                                                                                    │
                           │                                                                                        │
            """,
            ScreenText.Rows(screen, home.Path, 3, 10)
        );

        gmd.Send("Down");
        gmd.WaitFor("~/code/gmd   ↑↓");
        gmd.Send("Down");
        gmd.WaitFor("~/code/notes   ↑↓");
        gmd.Send("Right");
        gmd.WaitFor("│ │ │ ▸ drafts");
        gmd.Send("Left");
        gmd.WaitUntilGone("drafts");
        gmd.Send("Left");
        gmd.WaitFor("~/code   ↑↓");
    }

    // Typing finds a folder among those listed; → goes into it, its path typed, which lists it and
    // the folders in it; and Enter picks it, which Init then makes the repository in
    [TestMethod]
    public void TestTypingFindsAFolderToPick()
    {
        var home = Home();
        Directory.CreateDirectory(In(home, "code/notes"));
        Directory.CreateDirectory(In(home, "code/web"));
        using var gmd = TmuxSession.StartGmd(home, In(home, "code"));
        gmd.WaitFor("Quit");
        gmd.Send("i");
        gmd.WaitFor("Init Repo");
        gmd.Send("M-b");
        gmd.WaitFor("╭ Folder to Init");

        gmd.SendText("no");
        var found = gmd.WaitFor("1 of 5 folders   ↑↓ select  → into  Enter choose");
        StringAssert.Contains(found, "│ ~/code/notes  ");

        gmd.Send("Right");
        gmd.WaitFor("0 folders in ~/code/notes");
        gmd.Send("Enter");
        gmd.WaitUntilGone("Folder to Init");
        gmd.Send("Enter");

        gmd.WaitUntilGone("Quit");
        Assert.IsTrue(Directory.Exists(In(home, "code/notes/.git")), "Made in the folder picked");
    }

    // A whole path typed lists the folder it names, wherever it is, and Enter opens the repository
    [TestMethod]
    public async Task TestAPathTypedOpensTheRepository()
    {
        using var repo = await E2eRepo.CreateAsync();
        var home = Home();
        using var gmd = TmuxSession.StartGmd(home, In(home, "Downloads"));
        gmd.WaitFor("Quit");
        gmd.Send("o");
        gmd.WaitFor("Enter open");

        gmd.SendText(repo.Path);
        gmd.WaitFor($" in {Path.GetDirectoryName(repo.Path)}   ↑↓");
        gmd.Send("Enter");

        gmd.WaitFor("Add delta");
    }

    static TempHome Home(params string[] repos)
    {
        var home = TempHome.Create();
        Directory.CreateDirectory(In(home, "Downloads"));
        foreach (var repo in repos)
            Directory.CreateDirectory(Path.Join(In(home, repo), ".git"));
        return home;
    }

    static string In(TempHome home, string path) => Path.Join(home.Path, path);
}
