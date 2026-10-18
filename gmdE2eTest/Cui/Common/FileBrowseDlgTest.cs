using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui.Common;

// The file browser, which Blame File and File History pick their file in: the files of the
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

    // The file the commit added is listed first, under a heading that says so, with its letter and
    // the cursor on it, so Enter blames it; then every file as a tree, all closed, where it is marked
    // too. The status line says what the files are and what Enter does.
    [TestMethod]
    public async Task TestTheBrowserOpensOnTheFilesTheCommitChanged()
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
                           │ Changed in commit 7b9d57d6 ──────────────────────────────────────────────────────────  │
                           │   src/app/main.c                                                                    A  │
                           │ All files ───────────────────────────────────────────────────────────────────────────  │
                           │ ▸ src                                                                               •  │
                           │   alpha.txt                                                                            │
                           │   beta.txt                                                                             │
                           │   delta.txt                                                                            │
                           │   dev.txt                                                                              │
                           │   gamma.txt                                                                            │
                           │                                                                                        │
                           │                                                                                        │
                           │ 6 files of commit 7b9d57d6   ↑↓ select  ←→ folders  Enter blame                        │
                           ╰────────────────────────────────────────────────────────────────────────────────────────╯
            """,
            ScreenText.Rows(gmd.WaitFor("Enter blame"), repo.Path, 12, 16)
        );

        gmd.Send("Enter");
        gmd.WaitFor("Blame  src/app/main.c");
    }

    // ↓ goes past the heading into the tree, → opens a folder and goes into it, ← closes it; typing
    // finds a file instead, and Enter shows its history
    [TestMethod]
    public async Task TestTheArrowsOpenFoldersAndTypingFindsAFile()
    {
        using var repo = await CreateRepoAsync();
        using var gmd = TmuxSession.StartGmd(repo);
        gmd.WaitFor("Add main");
        OpenFromCommitMenu(gmd, "File History");
        gmd.WaitFor("Enter history");

        gmd.Send("Down");
        gmd.WaitFor("Enter open");
        gmd.Send("Right");
        gmd.WaitFor("│ ▸ app");
        gmd.Send("Right");
        gmd.WaitForStable();
        gmd.Send("Right");
        gmd.WaitFor("│ │   main.c");
        gmd.Send("Left");
        var closed = gmd.WaitUntilGone("│ │   main.c");
        Assert.AreEqual(
            """
                           ╭ File History ──────────────────────────────────────────────────────────────────────────╮
                           │ Find:                                                                                ] │
                           │────────────────────────────────────────────────────────────────────────────────────────│
                           │ Changed in commit 7b9d57d6 ──────────────────────────────────────────────────────────  │
                           │   src/app/main.c                                                                    A  │
                           │ All files ───────────────────────────────────────────────────────────────────────────  │
                           │ ▾ src                                                                               •  │
                           │ │ ▸ app                                                                             •  │
                           │   alpha.txt                                                                            │
                           │   beta.txt                                                                             │
                           │   delta.txt                                                                            │
                           │   dev.txt                                                                              │
                           │   gamma.txt                                                                            │
                           │                                                                                        │
                           │ 6 files of commit 7b9d57d6   ↑↓ select  ←→ folders  Enter open                         │
                           ╰────────────────────────────────────────────────────────────────────────────────────────╯
            """,
            ScreenText.Rows(closed, repo.Path, 12, 16)
        );

        gmd.SendText("gam");
        var found = gmd.WaitFor("1 of 6 files");
        StringAssert.Contains(found, "gamma.txt");
        gmd.Send("Enter");

        var history = gmd.WaitUntilGone("Find:");
        StringAssert.Contains(history, "Add gamma", "The history of gamma.txt");
    }

    // Opens the item of the commit menu, the last two of which are Blame File and File History:
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
        if (item == "File History")
        {
            gmd.Send("Up");
            gmd.WaitForStable();
        }
        gmd.Send("Enter");
        gmd.WaitFor($"╭ {item} ");
    }
}
