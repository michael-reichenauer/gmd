using gmd.Git.Private;
using gmdTest.Utils;

namespace gmdTest.Git;

// The key/value service is how gmd shares the user's branch choices: a value is stored as a plain
// git object, and pushed to a ref under 'refs/gmd-metadata-key-value/' on origin, so that it can be
// pushed and fetched like a branch.
[TestClass]
public class KeyValueServiceTest
{
    const string KeyRef = "refs/gmd-metadata-key-value/data";
    const string OriginRef = "refs/gmd-metadata-key-value-origin/data";

    // PushValueAsync writes the value to a temp file under .git before handing it to git
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

        var value = AssertOk(await new KeyValueService(cmd).GetValueAsync("data", wd));

        Assert.AreEqual("the stored value", value);
        Assert.AreEqual($"cat-file -p {KeyRef}", cmd.Calls[0].Args);
    }

    // A key that was never set is an error, which MetaDataService turns into empty metadata
    [TestMethod]
    public async Task TestGetValueOfUnknownKeyIsAnError()
    {
        var cmd = new FakeCmd((_, _, _) => FakeCmd.Fail($"fatal: Not a valid object name {KeyRef}"));

        var result = await new KeyValueService(cmd).GetValueAsync("data", wd);

        AssertError(result, "Expected the git failure to propagate");
    }

    // Removed, also when there is none, which git does not count a failure
    [TestMethod]
    public async Task TestDeleteValueDeletesTheRef()
    {
        var cmd = new FakeCmd("");

        AssertOk(await new KeyValueService(cmd).DeleteValueAsync("data", wd));

        Assert.AreEqual($"update-ref -d {KeyRef}", cmd.Calls[0].Args);
    }

    // Push is forced, since the value is a merge of the local and remote data rather than a fast
    // forward of it, but leased on origin's value as last fetched or pushed. The value goes into the
    // git object database, with no ref of the repository's own, and the object is pushed and recorded
    // as origin's.
    [TestMethod]
    public async Task TestPushValue()
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
                return FakeCmd.Ok(args.Contains("--quiet") ? "lastorigin456\n" : "");
            }
        );

        Assert.IsTrue(AssertOk(await new KeyValueService(cmd).PushValueAsync("data", "the value", wd)));

        Assert.AreEqual("the value", written);
        StringAssert.StartsWith(cmd.Calls[0].Args, "hash-object -w ");
        CollectionAssert.AreEqual(
            new[]
            {
                $"rev-parse --verify --quiet {OriginRef}",
                $"push --porcelain origin --force-with-lease={KeyRef}:lastorigin456 objectid123:{KeyRef}",
                $"update-ref {OriginRef} objectid123",
            },
            cmd.Calls.Skip(1).Select(c => c.Args).ToArray(),
            "The object id is trimmed"
        );
    }

    // In a linked worktree '.git' is a file pointing at the real git dir, so the temp file has to
    // go there — joining '.git' blindly would try to write inside a file
    [TestMethod]
    public async Task TestPushValueWritesItsTempFileInTheRealGitDirWhenDotGitIsAFile()
    {
        var realGitDir = Path.Join(wd, "real-git-dir");
        Directory.CreateDirectory(realGitDir);
        Directory.Delete(Path.Join(wd, ".git"), true);
        File.WriteAllText(Path.Join(wd, ".git"), $"gitdir: {realGitDir}\n");
        var tempPath = "";
        var cmd = new FakeCmd(
            (_, args, _) =>
            {
                if (args.StartsWith("hash-object"))
                    tempPath = args[(args.IndexOf('"') + 1)..].TrimSuffix("\"");
                return FakeCmd.Ok("objectid123\n");
            }
        );

        AssertOk(await new KeyValueService(cmd).PushValueAsync("data", "the value", wd));

        Assert.AreEqual(Path.GetFullPath(realGitDir), Path.GetDirectoryName(tempPath));
        Assert.AreEqual(0, Directory.GetFiles(realGitDir).Length, "The temp file is removed");
    }

    // The temp file must not be left behind in .git
    [TestMethod]
    public async Task TestPushValueRemovesItsTempFile()
    {
        var cmd = new FakeCmd("objectid123\n");

        await new KeyValueService(cmd).PushValueAsync("data", "the value", wd);

        Assert.AreEqual(0, Directory.GetFiles(Path.Join(wd, ".git")).Length);
    }

    [TestMethod]
    public async Task TestPushValueRemovesItsTempFileAlsoWhenGitFails()
    {
        var cmd = new FakeCmd((_, _, _) => FakeCmd.Fail("fatal: not a git repository"));

        var result = await new KeyValueService(cmd).PushValueAsync("data", "the value", wd);

        AssertError(result, "Expected the git failure to propagate");
        Assert.AreEqual(0, Directory.GetFiles(Path.Join(wd, ".git")).Length);
        Assert.AreEqual(1, cmd.Calls.Count, "Nothing is pushed");
    }

    // With no record of origin's value, the lease is on origin having none
    [TestMethod]
    public async Task TestAFirstPushIsLeasedOnOriginHavingNone()
    {
        var cmd = new FakeCmd(
            (_, args, _) => args.Contains("--quiet") ? FakeCmd.Fail("") : FakeCmd.Ok("objectid123\n")
        );

        AssertOk(await new KeyValueService(cmd).PushValueAsync("data", "the value", wd));

        Assert.AreEqual(
            $"push --porcelain origin --force-with-lease={KeyRef}: objectid123:{KeyRef}",
            cmd.Calls[2].Args
        );
    }

    // Another clone pushed since the last fetch: the push is refused, says so, and records nothing
    [TestMethod]
    public async Task TestAPushOverAValuePushedSinceIsRefused()
    {
        var cmd = new FakeCmd(
            (_, args, _) =>
                args.StartsWith("push")
                    ? new CmdResult("git push", 1, $"!\tobjectid123:{KeyRef}\t[rejected] (stale info)\nDone", "")
                    : FakeCmd.Ok("objectid123\n")
        );

        Assert.IsFalse(AssertOk(await new KeyValueService(cmd).PushValueAsync("data", "the value", wd)));

        Assert.IsFalse(cmd.Calls.Any(c => c.Args.StartsWith("update-ref")));
    }

    // A push that fails is not recorded as origin's value
    [TestMethod]
    public async Task TestAFailedPushRecordsNothing()
    {
        var cmd = new FakeCmd(
            (_, args, _) =>
                args.StartsWith("push") ? FakeCmd.Fail("fatal: unable to access") : FakeCmd.Ok("objectid123\n")
        );

        AssertError(await new KeyValueService(cmd).PushValueAsync("data", "the value", wd));

        Assert.IsFalse(cmd.Calls.Any(c => c.Args.StartsWith("update-ref")));
    }

    // Into the record of origin's value, never over the repository's own
    [TestMethod]
    public async Task TestFetchValue()
    {
        var cmd = new FakeCmd("");

        await new KeyValueService(cmd).FetchValueAsync("data", wd);

        Assert.AreEqual($"fetch origin +{KeyRef}:{OriginRef}", cmd.Calls[0].Args);
    }

    // Origin has none, e.g. it was deleted there: the record of it goes too, so that a push is leased
    // on origin having none, rather than on a value it no longer has, which was refused every time
    [TestMethod]
    public async Task TestFetchOfNoValueRemovesTheRecord()
    {
        var cmd = new FakeCmd(
            (_, args, _) =>
                args.StartsWith("fetch")
                    ? new CmdResult("git fetch", 128, "", $"fatal: couldn't find remote ref {KeyRef}")
                    : FakeCmd.Ok("")
        );

        AssertError(await new KeyValueService(cmd).FetchValueAsync("data", wd), "Still says there is none");

        Assert.AreEqual($"update-ref -d {OriginRef}", cmd.Calls[1].Args);
    }

    // ... but not when the fetch failed for another reason, which says nothing of origin's value
    [TestMethod]
    public async Task TestAFailedFetchKeepsTheRecord()
    {
        var cmd = new FakeCmd((_, _, _) => FakeCmd.Fail("fatal: unable to access"));

        AssertError(await new KeyValueService(cmd).FetchValueAsync("data", wd));

        Assert.AreEqual(1, cmd.Calls.Count);
    }

    [TestMethod]
    public async Task TestGetOriginValue()
    {
        var cmd = new FakeCmd("{}");

        Assert.AreEqual("{}", AssertOk(await new KeyValueService(cmd).GetOriginValueAsync("data", wd)));
        Assert.AreEqual($"cat-file -p {OriginRef}", cmd.Calls[0].Args);
    }
}
