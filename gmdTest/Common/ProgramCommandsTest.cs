using gmd.Common;

namespace gmdTest.Common;

// The command line options handled before the UI runs, and the ones left to the UI
[TestClass]
public class ProgramCommandsTest
{
    // The options the UI takes are no command, so the UI runs. The updater is never reached with
    // these.
    [TestMethod]
    [DataRow(new string[] { })]
    [DataRow(new[] { "-d", "/some/repo" })]
    [DataRow(new[] { "-m" })]
    public async Task TestUiOptionsAreNoCommand(string[] args)
    {
        var commands = new ProgramCommands(null!);

        Assert.IsNull(await commands.HandleCommandsAsync(args));
    }

    [TestMethod]
    public async Task TestVersionIsACommand()
    {
        var commands = new ProgramCommands(null!);

        Assert.AreEqual(0, await commands.HandleCommandsAsync(["--version"]));
    }

    // The folder of '-d <path>'
    [TestMethod]
    [DataRow(new[] { "-d", "/repo" }, "/repo")]
    [DataRow(new[] { "-d" }, null)]
    [DataRow(new[] { "-d", "-m" }, null)]
    [DataRow(new[] { "-m", "-d", "/repo" }, "/repo")]
    public void TestOptionValue(string[] args, string? expected)
    {
        Assert.AreEqual(expected, ProgramCommands.OptionValue(args, "-d"));
    }
}
