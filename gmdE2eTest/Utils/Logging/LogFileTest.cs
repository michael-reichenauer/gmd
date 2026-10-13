using gmdE2eTest.Fixtures;

namespace gmdE2eTest.Utils.Logging;

// The log file, ~/gmd.log, as the gmd that writes it sees it
//
// What every end-to-end test must keep doing is at the top of gmdE2eTest/TestSetup.cs.
[TestClass]
public class LogFileTest
{
    // A log gmd cannot write, as one that a 'sudo gmd' leaves owned by root, used to leave gmd
    // hanging on a blank screen, its first log line deadlocked against the log's own constructor.
    // It is replaced instead, and gmd starts and logs as ever.
    [TestMethod]
    public async Task TestGmdStartsWhenItsLogIsReadOnly()
    {
        if (Environment.IsPrivilegedProcess)
            Assert.Inconclusive("Root writes a read-only file all the same");
        using var repo = await E2eRepo.CreateAsync();
        var home = TempHome.Create();
        var log = Path.Join(home.Path, "gmd.log");
        File.WriteAllText(log, "A line of the last run\n");
        File.SetUnixFileMode(log, UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        using var gmd = TmuxSession.StartGmd(home, repo.Path);

        gmd.WaitFor("Initial");
        var text = File.ReadAllText(log);
        StringAssert.Contains(text, "Starting gmd");
        Assert.IsFalse(text.Contains("A line of the last run"), "Begun anew");
    }
}
