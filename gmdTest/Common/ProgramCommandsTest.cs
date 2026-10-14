using gmd.Common;

namespace gmdTest.Common;

// The command line options handled before the UI runs, and the ones left to the UI
[TestClass]
public class ProgramCommandsTest
{
    // The options the UI takes are no command, so the UI runs. The updater and the server are never
    // reached with these.
    [TestMethod]
    [DataRow(new string[] { })]
    [DataRow(new[] { "-d", "/some/repo" })]
    [DataRow(new[] { "-m" })]
    public async Task TestUiOptionsAreNoCommand(string[] args)
    {
        var commands = new ProgramCommands(null!, null!);

        Assert.IsNull(await commands.HandleCommandsAsync(args));
    }

    [TestMethod]
    public async Task TestVersionIsACommand()
    {
        var commands = new ProgramCommands(null!, null!);

        Assert.AreEqual(0, await commands.HandleCommandsAsync(["--version"]));
    }

    // CI names the release it is about to make, 'gmd --updatechangelog v1.6.1444.518'
    [TestMethod]
    [DataRow(new[] { "--updatechangelog", "v1.6" }, "v1.6")]
    [DataRow(new[] { "--updatechangelog" }, null)]
    [DataRow(new[] { "--updatechangelog", "-d", "/repo" }, null)]
    [DataRow(new[] { "-d", "/repo", "--updatechangelog", "v1.6" }, "v1.6")]
    public void TestOptionValue(string[] args, string? expected)
    {
        Assert.AreEqual(expected, ProgramCommands.OptionValue(args, "--updatechangelog"));
    }
}
