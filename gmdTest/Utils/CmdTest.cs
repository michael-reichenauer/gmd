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
