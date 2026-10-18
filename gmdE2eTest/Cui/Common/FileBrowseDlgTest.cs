using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui.Common;

// The file browser, which Blame File and Full File History pick their file in: the files of the
// row's commit as a tree, opened at the file the commit changed, the arrow keys opening and closing
// its folders, and typing to find a file instead. What the tree and the finder list, and in what
// order, is FileTreeTest's and FileFinderTest's.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class FileBrowseDlgTest
{
    // The fixture with a commit on top that adds a file two folders down
    static async Task<TempRepo> CreateRepoAsync()
    {
        var repo = await E2eRepo.CreateAsync();
        await repo.CommitFileAtAsync("src/app/main.c", "int main;\n", "Add main", TempRepo.BaseTime.AddMinutes(7));
        return repo;
    }

    // The folders of the file the commit added are open, and the cursor is on it, marked with its
    // letter, so Enter blames it. The status line says what the files are and what Enter does.
    [TestMethod]
    public async Task TestTheBrowserOpensAtTheFileTheCommitChanged()
    {
        using var repo = await CreateRepoAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Add main");

        OpenFromCommitMenu(gmd, "Blame File");

        Assert.AreEqual(
            """
                           ╭ Blame File ────────────────────────────────────────────────────────────────────────────╮
                           │ Find:                                                                                ] │
                           │────────────────────────────────────────────────────────────────────────────────────────│
                           │ ▾ src                                                                               •  │
                           │ │ ▾ app                                                                             •  │
                           │ │ │   main.c                                                                        A  │
                           │   alpha.txt                                                                            │
                           │   beta.txt                                                                             │
                           │   delta.txt                                                                            │
                           │   dev.txt                                                                              │
                           │   gamma.txt                                                                            │
                           │ 6 files of commit 7b9d57d6   ↑↓ select  ←→ folders  Enter blame                        │
                           ╰────────────────────────────────────────────────────────────────────────────────────────╯
            """,
            ScreenText.Rows(gmd.WaitFor("Enter blame"), repo.Path, 13, 13)
        );

        gmd.Send("Enter");
        gmd.WaitFor("Blame  src/app/main.c");
    }

    // ← goes up to the folder and closes it, → opens it again; typing finds a file instead, and
    // Enter shows its history
    [TestMethod]
    public async Task TestTheArrowsOpenFoldersAndTypingFindsAFile()
    {
        using var repo = await CreateRepoAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Add main");
        OpenFromCommitMenu(gmd, "Full File History");
        gmd.WaitFor("Enter history");

        gmd.Send("Left");
        gmd.WaitFor("Enter close");
        gmd.Send("Left");
        var closed = gmd.WaitUntilGone("main.c");
        Assert.AreEqual(
            """
                           │ ▾ src                                                                               •  │
                           │ │ ▸ app                                                                             •  │
                           │   alpha.txt                                                                            │
                           │   beta.txt                                                                             │
                           │   delta.txt                                                                            │
                           │   dev.txt                                                                              │
                           │   gamma.txt                                                                            │
                           │                                                                                        │
            """,
            ScreenText.Rows(closed, repo.Path, 16, 8)
        );
        gmd.Send("Right");
        gmd.WaitFor("main.c");

        gmd.SendText("gam");
        var found = gmd.WaitFor("1 of 6 files");
        StringAssert.Contains(found, "gamma.txt");
        gmd.Send("Enter");

        var history = gmd.WaitUntilGone("Find:");
        StringAssert.Contains(history, "Add gamma", "The history of gamma.txt");
    }

    // Opens the item of the commit menu, the last two of which are Blame File and Full File History:
    // 'End' and 'Up' are the steadier walk there than counting down past the items it skips
    static void OpenFromCommitMenu(TmuxSession gmd, string item)
    {
        gmd.Send("m");
        gmd.WaitFor("Commit ...");
        gmd.Send("End");
        gmd.WaitForStable();
        gmd.Send("Up");
        gmd.WaitForStable();
        gmd.Send("Up");
        gmd.WaitForStable();
        if (item == "Full File History")
        {
            gmd.Send("Up");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");
        gmd.WaitFor($"╭ {item} ");
    }
}
