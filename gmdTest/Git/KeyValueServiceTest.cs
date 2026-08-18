using gmd.Git.Private;
using gmdTest.Utils;

namespace gmdTest.Git;

// The key/value service is how gmd shares the user's branch choices: a value is stored as a plain
// git object, and a ref under 'refs/gmd-metadata-key-value/' points at it so it can be pushed and
// pulled like a branch.
[TestClass]
public class KeyValueServiceTest
{
    const string KeyRef = "refs/gmd-metadata-key-value/data";

    // SetValueAsync writes the value to a temp file under .git before handing it to git
    string wd = "";

    [TestInitialize]
    public void Init()
    {
        wd = Path.Join(Path.GetTempPath(), $"gmdTest-keyvalue-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Join(wd, ".git"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(wd))
            Directory.Delete(wd, true);
    }

    [TestMethod]
    public async Task TestGetValueReadsTheRef()
    {
        var cmd = new FakeCmd("the stored value");

        Assert.IsTrue(Try(out var value, out var e, await new KeyValueService(cmd).GetValueAsync("data", wd)), $"{e}");

        Assert.AreEqual("the stored value", value);
        Assert.AreEqual($"cat-file -p {KeyRef}", cmd.Calls[0].Args);
    }

    // A key that was never set is an error, which MetaDataService turns into empty metadata
    [TestMethod]
    public async Task TestGetValueOfUnknownKeyIsAnError()
    {
        var cmd = new FakeCmd((_, _, _) => FakeCmd.Fail($"fatal: Not a valid object name {KeyRef}"));

        var result = await new KeyValueService(cmd).GetValueAsync("data", wd);

        Assert.IsFalse(Try(out var _, out var _, result), "Expected the git failure to propagate");
    }

    // The value goes into the git object database, and the ref is then pointed at the new object
    [TestMethod]
    public async Task TestSetValueStoresTheValueAndUpdatesTheRef()
    {
        var written = "";
        var cmd = new FakeCmd(
            (_, args, _) =>
            {
                if (args.StartsWith("hash-object"))
                {
                    // The path is the last argument, quoted
                    var path = args[(args.IndexOf('"') + 1)..].TrimSuffix("\"");
                    written = File.ReadAllText(path);
                    return FakeCmd.Ok("objectid123\n");
                }
                return FakeCmd.Ok("");
            }
        );

        Assert.IsTrue(Try(out var e, await new KeyValueService(cmd).SetValueAsync("data", "the value", wd)), $"{e}");

        Assert.AreEqual("the value", written);
        StringAssert.StartsWith(cmd.Calls[0].Args, "hash-object -w ");
        Assert.AreEqual($"update-ref {KeyRef} objectid123", cmd.Calls[1].Args, "The object id is trimmed");
    }

    // The temp file must not be left behind in .git
    [TestMethod]
    public async Task TestSetValueRemovesItsTempFile()
    {
        var cmd = new FakeCmd("objectid123\n");

        await new KeyValueService(cmd).SetValueAsync("data", "the value", wd);

        Assert.AreEqual(0, Directory.GetFiles(Path.Join(wd, ".git")).Length);
    }

    [TestMethod]
    public async Task TestSetValueRemovesItsTempFileAlsoWhenGitFails()
    {
        var cmd = new FakeCmd((_, _, _) => FakeCmd.Fail("fatal: not a git repository"));

        var result = await new KeyValueService(cmd).SetValueAsync("data", "the value", wd);

        Assert.IsFalse(Try(out var _, result), "Expected the git failure to propagate");
        Assert.AreEqual(0, Directory.GetFiles(Path.Join(wd, ".git")).Length);
    }

    // Push is forced, since the value is a merge of the local and remote data rather than a
    // fast forward of it
    [TestMethod]
    public async Task TestPushValue()
    {
        var cmd = new FakeCmd("");

        await new KeyValueService(cmd).PushValueAsync("data", wd);

        Assert.AreEqual($"push --porcelain origin --set-upstream --force {KeyRef}:{KeyRef}", cmd.Calls[0].Args);
    }

    [TestMethod]
    public async Task TestPullValue()
    {
        var cmd = new FakeCmd("");

        await new KeyValueService(cmd).PullValueAsync("data", wd);

        Assert.AreEqual($"fetch origin {KeyRef}:{KeyRef}", cmd.Calls[0].Args);
    }
}
