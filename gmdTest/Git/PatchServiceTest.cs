using System.Text;
using System.Text.RegularExpressions;
using gmd.Git;
using gmd.Git.Private;
using gmdTest.Utils;

namespace gmdTest.Git;

// What git is asked to read a file's patch with, and to apply one with. Git writes the patch to the
// file named by --output, which the fake writes here as git would. PatchIntegrationTest has real git.
[TestClass]
public class PatchServiceTest
{
    const string Patch = "diff --git a/a.txt b/a.txt\n--- a/a.txt\n+++ b/a.txt\n@@ -1 +1 @@\n-x\n+y\n";

    // A fake git whose diff writes 'patch' to the file --output names, in a repository with a HEAD
    // unless not
    static FakeCmd GitWriting(byte[] patch, bool hasHead = true) =>
        new FakeCmd(
            (_, args, _) =>
            {
                if (args.StartsWith("rev-parse"))
                    return hasHead ? FakeCmd.Ok("1234abcd") : FakeCmd.Fail("", 1);
                if (!args.Contains(" diff "))
                    return FakeCmd.Ok("");
                File.WriteAllBytes(OutputOf(args), patch);
                return FakeCmd.Ok("");
            }
        );

    static string OutputOf(string args) => Regex.Match(args, "--output=\"([^\"]+)\"").Groups[1].Value;

    // Everything staged first, as the diff of the uncommitted changes stages it, so a file git does not
    // track yet is in it, but into a copy of the index; then whether the file was renamed, and the
    // diff of the path, taken as it is, not as a pattern, in the format the patch is read in, whatever
    // the user's config says
    [TestMethod]
    public async Task TestThePatchIsReadThroughACopyOfTheIndex()
    {
        var cmd = GitWriting(Encoding.UTF8.GetBytes(Patch));

        var patch = AssertOk(await new PatchService(cmd).GetUncommittedPatchAsync("a*.txt", "/wd"));

        Assert.AreEqual(Patch, patch.ToText());
        Assert.AreEqual("add -A", cmd.Calls[0].Args);
        Assert.AreEqual("rev-parse --verify --quiet HEAD", cmd.Calls[1].Args);
        Assert.AreEqual("diff --find-renames --name-status -z HEAD", cmd.Calls[2].Args);
        StringAssert.StartsWith(
            cmd.Calls[3].Args,
            "-c diff.suppressBlankEmpty=false diff --no-color --no-ext-diff --no-textconv"
        );
        StringAssert.EndsWith(
            cmd.Calls[3].Args,
            " --no-renames --output=\"" + OutputOf(cmd.Calls[3].Args) + "\" HEAD -- \"a*.txt\""
        );
        foreach (var call in cmd.Calls.Where(c => !c.Args.StartsWith("rev-parse")))
        {
            StringAssert.StartsWith(call.Environment!["GIT_INDEX_FILE"], Path.Join(Path.GetTempPath(), "gmd-index-"));
            Assert.AreEqual("1", call.Environment!["GIT_LITERAL_PATHSPECS"]);
        }
        Assert.IsFalse(File.Exists(OutputOf(cmd.Calls[3].Args)), "The patch file is deleted");
    }

    // A file renamed, moved with or without 'git mv', has the patch of both its paths, a rename, which
    // git finds as the diff of the uncommitted changes does
    [TestMethod]
    public async Task TestARenamedFileHasThePatchOfBothItsPaths()
    {
        var cmd = new FakeCmd(
            (_, args, _) =>
            {
                if (args.StartsWith("rev-parse"))
                    return FakeCmd.Ok("1234abcd");
                if (args.Contains("--name-status"))
                    return FakeCmd.Ok("M\0a.txt\0R075\0old.txt\0new.txt\0");
                if (args.Contains(" diff "))
                    File.WriteAllBytes(OutputOf(args), Encoding.UTF8.GetBytes(Patch));
                return FakeCmd.Ok("");
            }
        );

        AssertOk(await new PatchService(cmd).GetUncommittedPatchAsync("new.txt", "/wd"));

        StringAssert.Contains(cmd.Calls[3].Args, " --find-renames ");
        StringAssert.EndsWith(cmd.Calls[3].Args, " HEAD -- \"old.txt\" \"new.txt\"");
    }

    // A repo with no commit yet has no HEAD, and every file in it is new, which what is staged has.
    // Asked first: with a path git says 'bad revision', where without one it says 'unknown revision',
    // and the patch of the first commit's files was not read at all.
    [TestMethod]
    public async Task TestARepoWithNoCommitYetHasTheStagedPatch()
    {
        var cmd = GitWriting(Encoding.UTF8.GetBytes(Patch), hasHead: false);

        AssertOk(await new PatchService(cmd).GetUncommittedPatchAsync("a.txt", "/wd"));

        StringAssert.EndsWith(cmd.Calls[2].Args, " --staged -- \"a.txt\"");
    }

    // Bytes that are not UTF-8 would be changed by being read as text and written back, so the file
    // is taken whole only; and a file with no changes has no patch
    [TestMethod]
    public async Task TestAPatchThatIsNotUtf8OrIsEmptyIsNotRead()
    {
        var latin1 = Encoding.Latin1.GetBytes(Patch.Replace("+y", "+café"));

        var notUtf8 = AssertError(await new PatchService(GitWriting(latin1)).GetUncommittedPatchAsync("a.txt", "/wd"));
        var empty = AssertError(await new PatchService(GitWriting([])).GetUncommittedPatchAsync("a.txt", "/wd"));

        StringAssert.Contains(notUtf8.Message, "a.txt is not UTF-8 text");
        StringAssert.Contains(empty.Message, "a.txt has no uncommitted changes");
    }

    // To an index, a copy for "" being the repository's own, or in reverse to the working tree; the
    // patch as it is written, its whitespace applied as it is whatever apply.whitespace says, and an
    // empty one not applied at all
    [TestMethod]
    public async Task TestAPatchIsAppliedToAnIndexOrInReverse()
    {
        var written = "";
        var cmd = new FakeCmd(
            (_, args, _) =>
            {
                written = File.ReadAllText(Regex.Match(args, "\"([^\"]+)\"$").Groups[1].Value);
                return FakeCmd.Ok("");
            }
        );
        var service = new PatchService(cmd);
        var patch = AssertOk(PatchParser.Parse(Patch));

        AssertOk(await service.ApplyToIndexAsync(patch, "/tmp/index", "/wd"));
        Assert.AreEqual(Patch, written);
        AssertOk(await service.ApplyToIndexAsync(patch, "", "/wd"));
        AssertOk(await service.ApplyReversedAsync(patch, "/wd"));
        AssertOk(await service.ApplyReversedAsync(AssertOk(patch.ForApply(_ => false)), "/wd"));

        StringAssert.StartsWith(cmd.Calls[0].Args, "apply --cached --whitespace=nowarn ");
        Assert.AreEqual("/tmp/index", cmd.Calls[0].Environment!["GIT_INDEX_FILE"]);
        Assert.IsNull(cmd.Calls[1].Environment);
        StringAssert.StartsWith(cmd.Calls[2].Args, "apply -R --whitespace=nowarn ");
        Assert.AreEqual(3, cmd.Calls.Count, "The empty patch is not applied");
    }
}
