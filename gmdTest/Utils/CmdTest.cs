using System.Text.RegularExpressions;
using gmd.Git.Private;

namespace gmdTest.Utils;

[TestClass]
public class CmdTest
{
    // A failed command records where it was run from as its origin, the service method that ran
    // it, rather than Cmd, which every command failure would otherwise share. A command that cannot
    // even be started is a failure too, which is what lets this use the real Cmd without running
    // anything.
    [TestMethod]
    public async Task TestCmdErrorOriginIsTheCaller()
    {
        ICmd cmd = new Cmd();
        var origin = new Regex($@"CmdTest\.cs\(\d+\) {nameof(TestCmdErrorOriginIsTheCaller)}");

        var e = AssertError(cmd.Command("gmd-test-no-such-command", "", ""));
        Assert.IsInstanceOfType<CmdError>(e);
        StringAssert.Matches(e.Origin, origin);

        var asyncError = AssertError(await cmd.RunAsync("gmd-test-no-such-command", "", ""));
        StringAssert.Matches(asyncError.Origin, origin, "Across the thread RunAsync runs it on");
    }

    // Git is run in English whatever the user's language, since gmd recognizes what happened by
    // git's messages. LANGUAGE is what gettext reads first, so it wins over the user's own.
    [TestMethod]
    public void TestCommandsRunInEnglish()
    {
        if (OperatingSystem.IsWindows())
            Assert.Inconclusive("Uses sh");

        Assert.AreEqual("en", AssertOk(new Cmd().Command("sh", "-c \"echo $LANGUAGE\"", "")));
    }

    // StartAsync is for a program that may go on running, a browser: one that fails at once is an
    // error with what it said, as for any command
    [TestMethod]
    public async Task TestStartReportsAProgramThatFailsAtOnce()
    {
        if (OperatingSystem.IsWindows())
            Assert.Inconclusive("Uses sh");

        var e = AssertError(await new Cmd().StartAsync("sh", "-c \"echo oops >&2; exit 4\""));

        Assert.AreEqual(4, ((CmdError)e).ExitCode);
        StringAssert.Contains(e.Message, "oops");
        AssertError(await new Cmd().StartAsync("gmd-test-no-such-command", ""), "Not installed");
    }

    // All of what it said, however much: the error output is read on a thread of its own, which
    // can still be at it when the program is seen to have exited. Ten times, since it is a race.
    [TestMethod]
    public async Task TestStartReportsAllThatAFailingProgramSaid()
    {
        if (OperatingSystem.IsWindows())
            Assert.Inconclusive("Uses sh");

        for (int i = 0; i < 10; i++)
        {
            var e = AssertError(await new Cmd().StartAsync("sh", "-c \"seq 1 2000 >&2; echo last >&2; exit 4\""));
            StringAssert.Contains(e.Message, "last");
        }
    }

    // One still running after the wait has worked, as a browser started in the foreground has, and
    // is left running: killing it on a timeout, as CommandWithStdin does, would close the browser
    [TestMethod]
    [TestCategory("Integration")]
    public async Task TestStartLeavesAProgramStillRunning()
    {
        if (OperatingSystem.IsWindows())
            Assert.Inconclusive("Uses sh");
        var done = Path.Join(Path.GetTempPath(), $"gmdTest-start-{Guid.NewGuid():N}");

        AssertOk(await new Cmd().StartAsync("sh", $"-c \"sleep 2; touch '{done}'\""));

        Assert.IsTrue(WaitForFile(done), "It ran to its end rather than being killed");
        File.Delete(done);
    }

    // Its output is read while it runs. A pipe that is never read blocks the program once it is
    // full, some 64 KiB, which a browser writing its logs soon is.
    [TestMethod]
    public async Task TestStartDoesNotBlockAProgramThatWritesALot()
    {
        if (OperatingSystem.IsWindows())
            Assert.Inconclusive("Uses sh");
        var done = Path.Join(Path.GetTempPath(), $"gmdTest-start-{Guid.NewGuid():N}");

        AssertOk(
            await new Cmd().StartAsync(
                "sh",
                $"-c \"head -c 1000000 /dev/zero; head -c 1000000 /dev/zero >&2; touch '{done}'\""
            )
        );

        Assert.IsTrue(WaitForFile(done), "It wrote everything and went on");
        File.Delete(done);
    }

    static bool WaitForFile(string path)
    {
        for (int i = 0; i < 100 && !File.Exists(path); i++)
            Thread.Sleep(50);
        return File.Exists(path);
    }

    // Which is what makes it useful: through a git service the origin names that service
    [TestMethod]
    public async Task TestCmdErrorOriginNamesTheGitService()
    {
        var cmd = new FakeCmd((_, _, _) => FakeCmd.Fail("fatal: Not a valid object name"));
        var keyValues = new KeyValueService(cmd);

        var e = AssertError(await keyValues.GetValueAsync("key", "/wd"));

        StringAssert.Matches(
            e.Origin,
            new Regex($@"KeyValueService\.cs\(\d+\) {nameof(KeyValueService.GetValueAsync)}")
        );
    }
}
