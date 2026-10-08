using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Cui;

// gmd in an 80 by 24 terminal, the classic size and still many a terminal's default: it need not
// look its best there, but everything must be usable, i.e. on the screen as a whole, frame, buttons
// and all. The rest of the suite runs at 120 by 40, where nothing of this shows.
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class SmallTerminalTest
{
    const int Width = 80;
    const int Height = 24;

    // A dialog or menu is whole when its top row has both corners and a row below it the bottom
    // edge, i.e. it was not cut at the right or the bottom of the screen
    static void AssertWhole(string screen, string title)
    {
        var rows = screen.Split('\n');
        var top = Array.FindIndex(rows, r => r.Contains($"╭ {title}"));
        Assert.IsTrue(top >= 0, $"No '{title}' on the screen:\n{screen}");
        StringAssert.Contains(rows[top][rows[top].IndexOf('╭')..], "╮", $"'{title}' is cut at the right:\n{screen}");
        Assert.IsTrue(
            rows.Skip(top + 1).Any(r => r.Contains('╰') && r.Contains('╯')),
            $"'{title}' is cut at the bottom:\n{screen}"
        );
    }

    [TestMethod]
    public async Task TestTheLogTheMenuAndTheHelpFit()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo, Width, Height);

        var log = gmd.WaitFor("Initial");
        StringAssert.StartsWith(log, " Gmd ");

        gmd.Send("m");
        AssertWhole(gmd.WaitFor("Commit ..."), "Commit:");
        gmd.Send("Escape");
        gmd.WaitUntilGone("Commit ...");

        // The box with the link to the help, which is wider than the screen and wrapped in it
        gmd.Send("?");
        AssertWhole(gmd.WaitFor("copied to the clipboard"), "Help");
    }

    // The file list is shorter on a small terminal, and scrolls, so the buttons stay on the screen.
    // It used to grow the dialog past the bottom, with OK and Cancel out of sight.
    [TestMethod]
    public async Task TestTheCommitDialogFitsWithManyFiles()
    {
        using var repo = await E2eRepo.CreateWithChangesAsync();
        for (int i = 0; i < 10; i++)
            File.WriteAllText(Path.Join(repo.Path, $"file{i}.txt"), $"{i}\n");
        using var gmd = TmuxSession.StartGmd(repo, Width, Height);
        gmd.WaitFor("uncommitted");

        gmd.Send("c");
        var dialog = gmd.WaitFor("Commit 12 changes");

        AssertWhole(dialog, "Commit");
        StringAssert.Contains(dialog, "[ Cancel ]");
    }

    // The list dialogs are as wide as their columns need, which Recover Lost Commits made 83 columns
    [TestMethod]
    public async Task TestAListDialogFits()
    {
        using var repo = await E2eRepo.CreateAsync();
        await repo.CommitFileAtAsync("lost.txt", "lost\n", "Lost work", TempRepo.BaseTime.AddMinutes(7));
        await repo.GitAsync("reset -q --hard HEAD~1");
        using var gmd = TmuxSession.StartGmd(repo, Width, Height);
        gmd.WaitFor("Initial");

        // As in LostWorkDlgTest: Undo is three moves down, and the item one into it
        gmd.Send("m");
        gmd.WaitFor("Commit ...");
        for (var i = 0; i < 3; i++)
        {
            gmd.Send("Down");
            gmd.WaitForStable();
        }
        gmd.Send("Right");
        gmd.WaitFor("Recover Lost Commits");
        gmd.Send("Down");
        gmd.WaitForStable();
        gmd.Send("Enter");

        var dialog = gmd.WaitFor("Lost work");
        AssertWhole(dialog, "Recover Lost Commits");
        StringAssert.Contains(dialog, "[ Close ]");
    }

    // The side by side diff halves the width, which leaves each side 38 columns
    [TestMethod]
    public async Task TestTheDiffIsShown()
    {
        using var repo = await E2eRepo.CreateAsync();
        using var gmd = TmuxSession.StartGmd(repo, Width, Height);
        gmd.WaitFor("Initial");

        gmd.Send("d");

        StringAssert.Contains(gmd.WaitFor("Added: delta.txt"), "delta");
    }
}
