using System.Security.Cryptography;
using System.Text;
using gmd.Git;
using gmdTest.Fixtures;

namespace gmdTest.Git;

// Patches of some of a file's changes, applied by git: to an index of HEAD, which is how a part of a
// file will be committed, and in reverse to the working tree, which is how a part will be discarded.
// What git makes of them is held to a model with nothing of FilePatch's counting in it: the lines of
// the whole file, from a patch with all of them as context, with the chosen changes made or taken
// back, a change's removed and added lines paired as the diff view pairs them. FilePatchTest has the
// patches themselves.
//
// The tests are in their own category, so they can be excluded when only the fast tests are
// wanted:  scripts/test --filter "TestCategory!=Integration"
[TestClass]
[TestCategory("Integration")]
public class PatchIntegrationTest
{
    TempRepo repo = null!;

    [TestInitialize]
    public async Task Init() => repo = await TempRepo.CreateAsync();

    [TestCleanup]
    public void Cleanup() => repo.Dispose();

    // Seven changed lines in three places of a file: two lines changed and one added after them, a
    // line removed close enough to be in the same hunk, and one removed in a hunk of its own, which
    // starts where the changes chosen before it put it. Every one of the 128 choices of them, applied
    // to HEAD and taken back from the working tree, makes the file the model says.
    [TestMethod]
    public async Task TestEveryChoiceOfAFilesChangesIsAppliedAsChosen()
    {
        var lines = Enumerable.Range(1, 20).Select(i => $"line {i}").ToList();
        await repo.CommitFileAsync("a.txt", Text(lines), "Lines");
        lines[2] = "LINE 3";
        lines[3] = "LINE 4";
        lines.Insert(4, "added");
        lines.Remove("line 9");
        lines.Remove("line 17");
        repo.WriteFile("a.txt", Text(lines));
        var newBytes = await File.ReadAllBytesAsync(PathOf("a.txt"));

        var patch = AssertOk(await repo.Git.GetUncommittedPatchAsync("a.txt", repo.Path));
        var full = FullPatch("a.txt");
        var changes = patch.Changes.Select(Key).ToList();
        Assert.AreEqual("-3, -4, +3, +4, +5, -9, -17", string.Join(", ", changes));
        Assert.AreEqual(2, patch.Hunks.Count);
        CollectionAssert.AreEqual(
            changes,
            full.Changes.Select(Key).ToList(),
            "The same, in one hunk of the whole file"
        );

        for (var set = 0; set < 1 << changes.Count; set++)
        {
            var chosen = changes.Where((_, i) => (set & 1 << i) != 0).ToHashSet();
            bool IsChosen(PatchLine l) => chosen.Contains(Key(l));
            var what = $"Chosen: {string.Join(", ", chosen)}";

            var index = await IndexWithAsync(AssertOk(patch.ForApply(IsChosen)));
            Assert.AreEqual(BlobId(Expected(full, IsChosen, isReverse: false)), index, $"Applied. {what}");

            AssertOk(await repo.Git.ApplyPatchReversedAsync(AssertOk(patch.ForReverseApply(IsChosen)), repo.Path));
            CollectionAssert.AreEqual(
                Expected(full, IsChosen, isReverse: true),
                File.ReadAllBytes(PathOf("a.txt")),
                $"Taken back. {what}"
            );
            await File.WriteAllBytesAsync(PathOf("a.txt"), newBytes);
        }
    }

    // A file git does not track yet is a new file, of which some lines make a new file of those lines,
    // and some taken back leave the rest in it; what the user staged is not touched by reading it
    [TestMethod]
    public async Task TestSomeLinesOfANewFile()
    {
        await repo.CommitFileAsync("other.txt", "other\n", "Other");
        repo.WriteFile("n.txt", "a\nb\nc\n");
        repo.WriteFile("other.txt", "staged\n");
        await repo.GitAsync("add other.txt");
        var staged = await repo.GitAsync("diff --cached");

        var patch = AssertOk(await repo.Git.GetUncommittedPatchAsync("n.txt", repo.Path));
        var index = await IndexWithAsync(AssertOk(patch.ForApply(l => l.Text != "b")));
        AssertOk(
            await repo.Git.ApplyPatchReversedAsync(AssertOk(patch.ForReverseApply(l => l.Text == "b")), repo.Path)
        );

        Assert.IsTrue(patch.IsNew);
        Assert.AreEqual(BlobId("a\nc\n"), index);
        Assert.AreEqual("a\nc\n", File.ReadAllText(PathOf("n.txt")));
        Assert.AreEqual(staged, await repo.GitAsync("diff --cached"), "What was staged is as it was");
        Assert.AreEqual("", await repo.GitAsync("ls-files n.txt"), "Not staged");
    }

    // A deleted file with some of its lines taken out of HEAD is a file of the rest, and with some put
    // back in the working tree a file of those
    [TestMethod]
    public async Task TestSomeLinesOfADeletedFile()
    {
        await repo.CommitFileAsync("d.txt", "a\nb\nc\n", "Lines");
        repo.DeleteFile("d.txt");

        var patch = AssertOk(await repo.Git.GetUncommittedPatchAsync("d.txt", repo.Path));
        var index = await IndexWithAsync(AssertOk(patch.ForApply(l => l.Text == "b")));
        AssertOk(
            await repo.Git.ApplyPatchReversedAsync(AssertOk(patch.ForReverseApply(l => l.Text == "b")), repo.Path)
        );

        Assert.IsTrue(patch.IsDeleted);
        Assert.AreEqual(BlobId("a\nc\n"), index);
        Assert.AreEqual("b\n", File.ReadAllText(PathOf("d.txt")));
    }

    // '\r\n' line ends and a last line with no newline are kept as they are, byte for byte
    [TestMethod]
    public async Task TestLineEndsAndNoNewlineAtTheEndAreKept()
    {
        await repo.CommitFileAsync("a.txt", "one\r\ntwo\r\nthree", "Lines");
        repo.WriteFile("a.txt", "one\r\nTWO\r\nthree\r\nfour");
        bool IsTwo(PatchLine l) => l.Text.StartsWith("two", StringComparison.OrdinalIgnoreCase);

        var patch = AssertOk(await repo.Git.GetUncommittedPatchAsync("a.txt", repo.Path));
        var index = await IndexWithAsync(AssertOk(patch.ForApply(IsTwo)));
        AssertOk(await repo.Git.ApplyPatchReversedAsync(AssertOk(patch.ForReverseApply(IsTwo)), repo.Path));

        Assert.AreEqual(BlobId("one\r\nTWO\r\nthree"), index);
        Assert.AreEqual("one\r\ntwo\r\nthree\r\nfour", File.ReadAllText(PathOf("a.txt")));
    }

    // With core.autocrlf git has '\n' in the repository and '\r\n' in the working tree, and the patch
    // is of the first, which git takes back in the working tree with the line ends it has there
    [TestMethod]
    public async Task TestAutoCrlfIsKeptInTheWorkingTree()
    {
        await repo.GitAsync("config core.autocrlf true");
        await repo.CommitFileAsync("a.txt", "one\r\ntwo\r\nthree\r\n", "Lines");
        repo.WriteFile("a.txt", "one\r\nTWO\r\nthree\r\nfour\r\n");

        var patch = AssertOk(await repo.Git.GetUncommittedPatchAsync("a.txt", repo.Path));
        AssertOk(
            await repo.Git.ApplyPatchReversedAsync(AssertOk(patch.ForReverseApply(l => l.Text != "four")), repo.Path)
        );

        Assert.AreEqual("one\r\ntwo\r\nthree\r\nfour\r\n", File.ReadAllText(PathOf("a.txt")));
    }

    // A patch of a file that has changed since is not applied, nor any part of it: git applies all of
    // it or none
    [TestMethod]
    public async Task TestAPatchThatNoLongerFitsIsNotApplied()
    {
        await repo.CommitFileAsync("a.txt", "one\ntwo\nthree\nfour\n", "Lines");
        repo.WriteFile("a.txt", "ONE\ntwo\nthree\nFOUR\n");
        var patch = AssertOk(await repo.Git.GetUncommittedPatchAsync("a.txt", repo.Path));
        repo.WriteFile("a.txt", "ONE\nchanged since\nthree\nFOUR\n");

        var error = AssertError(
            await repo.Git.ApplyPatchReversedAsync(AssertOk(patch.ForReverseApply(_ => true)), repo.Path)
        );

        StringAssert.Contains(error.AllMessages(), "patch does not apply");
        Assert.AreEqual("ONE\nchanged since\nthree\nFOUR\n", File.ReadAllText(PathOf("a.txt")));
    }

    static string Text(IEnumerable<string> lines) => string.Concat(lines.Select(l => l + "\n"));

    string PathOf(string name) => Path.Join(repo.Path, name);

    // A change by what it is and where: '-3' the old version's line 3, '+9' the new version's line 9
    static string Key(PatchLine l) => l.Kind == PatchLineKind.Removed ? $"-{l.OldLineNbr}" : $"+{l.NewLineNbr}";

    // The patch of the file with all of its lines as context, which is the whole file in one hunk
    FilePatch FullPatch(string name)
    {
        var file = Path.Join(repo.Path, ".git", "full.diff");
        Proc.Ok(
            "git",
            [
                "diff",
                "--no-color",
                "--src-prefix=a/",
                "--dst-prefix=b/",
                "-U100000",
                $"--output={file}",
                "HEAD",
                "--",
                name,
            ],
            repo.Path
        );
        return AssertOk(gmd.Git.Private.PatchParser.Parse(File.ReadAllText(file)));
    }

    // The file with the chosen changes made to the old version, or taken back from the new one: of
    // each pair of a removed line and an added one, the removed line is in the file if it is not
    // chosen, or is to be put back, and the added line if it is chosen, or is not to be taken back
    static byte[] Expected(FilePatch full, Func<PatchLine, bool> isChosen, bool isReverse)
    {
        var all = full.Hunks.SelectMany(h => h.Lines).ToList();
        List<PatchLine> lines = [];
        for (var i = 0; i < all.Count; )
        {
            var change = all.Skip(i).TakeWhile(l => l.IsChange).ToList();
            if (change.Count == 0)
            {
                lines.Add(all[i++]);
                continue;
            }
            var removed = change.Where(l => l.Kind == PatchLineKind.Removed).ToList();
            var added = change.Where(l => l.Kind == PatchLineKind.Added).ToList();
            for (var row = 0; row < Math.Max(removed.Count, added.Count); row++)
            {
                if (row < removed.Count && isChosen(removed[row]) == isReverse)
                    lines.Add(removed[row]);
                if (row < added.Count && isChosen(added[row]) != isReverse)
                    lines.Add(added[row]);
            }
            i += change.Count;
        }
        return Encoding.UTF8.GetBytes(string.Concat(lines.Select(l => l.Text + (l.NoNewline == "" ? "\n" : ""))));
    }

    // The patch applied to a new index of HEAD, and the id of what that then has of the file
    async Task<string> IndexWithAsync(FilePatch patch)
    {
        var index = Path.Join(repo.Path, ".git", "test-index");
        var env = new Dictionary<string, string> { ["GIT_INDEX_FILE"] = index };
        try
        {
            Proc.Ok("git", ["read-tree", "HEAD"], repo.Path, env);
            AssertOk(await repo.Git.ApplyPatchToIndexAsync(patch, index, repo.Path));
            var path = patch.Header[^1][6..]; // '+++ b/a.txt'
            return Proc.Ok("git", ["rev-parse", $":{path}"], repo.Path, env).Trim();
        }
        finally
        {
            File.Delete(index);
        }
    }

    // The id git gives a file of these bytes, a SHA-1 of them with a header, as TempRepo makes repos
    static string BlobId(string text) => BlobId(Encoding.UTF8.GetBytes(text));

    static string BlobId(byte[] bytes)
    {
        var header = Encoding.ASCII.GetBytes($"blob {bytes.Length}\0");
        return Convert.ToHexStringLower(SHA1.HashData([.. header, .. bytes]));
    }
}
