using gmd.Git;
using gmd.Git.Private;
using gmdTest.Utils;

namespace gmdTest.Git;

[TestClass]
public class DiffServiceTest
{
    // Output of: git show --date=iso --first-parent --root --patch --no-color --find-renames
    //            --unified=6 HEAD
    // One commit touching a file of every kind: modified, binary, deleted, added and renamed.
    const string ShowOutput = """
        commit 134e1960d41fc44fb5ffffde38c2273f5e9910fc
        Author: Test <t@t.com>
        Date:   2026-07-31 03:55:44 +0200

            Second commit with all kinds of change

        diff --git a/a.txt b/a.txt
        index 4cb29ea..ddc897f 100644
        --- a/a.txt
        +++ b/a.txt
        @@ -1,3 +1,3 @@
         one
        -two
        +TWO
         three
        diff --git a/bin.dat b/bin.dat
        index d630fa5..370a411 100644
        Binary files a/bin.dat and b/bin.dat differ
        diff --git a/gone.txt b/gone.txt
        deleted file mode 100644
        index c118916..0000000
        --- a/gone.txt
        +++ /dev/null
        @@ -1 +0,0 @@
        -del me
        diff --git a/new.txt b/new.txt
        new file mode 100644
        index 0000000..d5a09df
        --- /dev/null
        +++ b/new.txt
        @@ -0,0 +1 @@
        +brand new
        diff --git a/oldname.txt b/newname.txt
        similarity index 100%
        rename from oldname.txt
        rename to newname.txt
        """;

    // Output of 'git diff HEAD' while a merge is stopped on conflicts: the conflict markers are
    // part of the working tree file, so they show up as added lines
    const string ConflictOutput = """
        diff --git a/addboth.txt b/addboth.txt
        index ac3b85d..3661d22 100644
        --- a/addboth.txt
        +++ b/addboth.txt
        @@ -1 +1,5 @@
        +<<<<<<< HEAD
         mainboth
        +=======
        +newboth
        +>>>>>>> topic
        diff --git a/mod-del.txt b/mod-del.txt
        new file mode 100644
        index 0000000..ad6254a
        --- /dev/null
        +++ b/mod-del.txt
        @@ -0,0 +1 @@
        +topicmod
        """;

    // A combined diff, i.e. 'git show --cc' of a merge commit. None of gmd's git commands ask for
    // one (they all use --first-parent), but the parser has a branch for it.
    const string CombinedOutput = """
        commit 1c6014174680fc2302e29dc5874a4ce5d39281fd
        Merge: 063d1bf 3883620
        Author: Test <t@t.com>
        Date:   2026-07-31 03:56:12 +0200

            Merge branch 'topic'

        diff --cc both.txt
        index ba2906d,0f62d67..2ab19ae
        --- a/both.txt
        +++ b/both.txt
        @@@ -1,1 -1,1 +1,1 @@@
        - main
         -topic
        ++resolved
        """;

    // Output of: git log --date=iso --patch --follow -- "a.txt", i.e. several commits in one output
    const string FileLogOutput = """
        commit 134e1960d41fc44fb5ffffde38c2273f5e9910fc
        Author: Test <t@t.com>
        Date:   2026-07-31 03:55:44 +0200

            Second commit with all kinds of change

        diff --git a/a.txt b/a.txt
        index 4cb29ea..ddc897f 100644
        --- a/a.txt
        +++ b/a.txt
        @@ -1,3 +1,3 @@
         one
        -two
        +TWO
         three

        commit ef1633d02c30d3e960d5b734f3c109d28010805c
        Author: Test <t@t.com>
        Date:   2026-07-31 03:55:44 +0200

            Initial commit

        diff --git a/a.txt b/a.txt
        new file mode 100644
        index 0000000..4cb29ea
        --- /dev/null
        +++ b/a.txt
        @@ -0,0 +1,3 @@
        +one
        +two
        +three
        """;

    static async Task<CommitDiff> GetCommitDiffAsync(string output)
    {
        var service = new DiffService(new FakeCmd(output));
        var result = await service.GetCommitDiffAsync("HEAD", "/wd");
        Assert.IsTrue(Try(out var commitDiff, out var e, result), $"GetCommitDiffAsync failed: {e}");
        return commitDiff;
    }

    static FileDiff FileOf(CommitDiff commitDiff, string path) =>
        commitDiff.FileDiffs.First(fd => fd.PathAfter == path);

    // Lines of a section as '<mode> <text>', which is compact enough to assert as one string
    static string LinesOf(SectionDiff section) =>
        string.Join("\n", section.LineDiffs.Select(l => $"{l.DiffMode} {l.Line}"));

    [TestMethod]
    public async Task TestParseCommitHeader()
    {
        var commitDiff = await GetCommitDiffAsync(ShowOutput);

        Assert.AreEqual("134e1960d41fc44fb5ffffde38c2273f5e9910fc", commitDiff.Id);
        Assert.AreEqual("Test <t@t.com>", commitDiff.Author);
        Assert.AreEqual("Second commit with all kinds of change", commitDiff.Message);
        // Compared as UTC so the assert does not depend on the machine's time zone
        Assert.AreEqual(new DateTime(2026, 7, 31, 1, 55, 44, DateTimeKind.Utc), commitDiff.Time.ToUniversalTime());
    }

    [TestMethod]
    public async Task TestParseAllFilesOfACommit()
    {
        var commitDiff = await GetCommitDiffAsync(ShowOutput);

        CollectionAssert.AreEqual(
            new[] { "a.txt", "bin.dat", "gone.txt", "new.txt", "newname.txt" },
            commitDiff.FileDiffs.Select(fd => fd.PathAfter).ToArray()
        );
    }

    [TestMethod]
    public async Task TestParseModifiedFile()
    {
        var file = FileOf(await GetCommitDiffAsync(ShowOutput), "a.txt");

        Assert.AreEqual(DiffMode.DiffModified, file.DiffMode);
        Assert.IsFalse(file.IsBinary);
        Assert.IsFalse(file.IsRenamed);
        Assert.AreEqual(1, file.SectionDiffs.Count);
        Assert.AreEqual(
            """
            DiffSame one
            DiffRemoved two
            DiffAdded TWO
            DiffSame three
            """,
            LinesOf(file.SectionDiffs[0])
        );
    }

    // 'Binary files a/… and b/… differ' replaces the hunks, so a binary file has no sections
    [TestMethod]
    public async Task TestParseBinaryFile()
    {
        var file = FileOf(await GetCommitDiffAsync(ShowOutput), "bin.dat");

        Assert.IsTrue(file.IsBinary);
        Assert.AreEqual(0, file.SectionDiffs.Count);
        Assert.AreEqual(DiffMode.DiffModified, file.DiffMode);
    }

    [TestMethod]
    public async Task TestParseDeletedFile()
    {
        var file = FileOf(await GetCommitDiffAsync(ShowOutput), "gone.txt");

        Assert.AreEqual(DiffMode.DiffRemoved, file.DiffMode);
        Assert.AreEqual("DiffRemoved del me", LinesOf(file.SectionDiffs[0]));
    }

    [TestMethod]
    public async Task TestParseAddedFile()
    {
        var file = FileOf(await GetCommitDiffAsync(ShowOutput), "new.txt");

        Assert.AreEqual(DiffMode.DiffAdded, file.DiffMode);
        Assert.AreEqual("DiffAdded brand new", LinesOf(file.SectionDiffs[0]));
    }

    // A pure rename has the 'similarity index'/'rename from'/'rename to' lines and no hunks
    [TestMethod]
    public async Task TestParseRenamedFile()
    {
        var file = FileOf(await GetCommitDiffAsync(ShowOutput), "newname.txt");

        Assert.IsTrue(file.IsRenamed);
        Assert.AreEqual("oldname.txt", file.PathBefore);
        Assert.AreEqual(DiffMode.DiffModified, file.DiffMode);
        Assert.AreEqual(0, file.SectionDiffs.Count);
    }

    // The hunk header '@@ -1,3 +1,3 @@' is kept as ChangedIndexes without the leading '-'
    [TestMethod]
    public async Task TestParseSectionIndexes()
    {
        var file = FileOf(await GetCommitDiffAsync(ShowOutput), "a.txt");
        var section = file.SectionDiffs[0];

        Assert.AreEqual("1,3 +1,3", section.ChangedIndexes);
        Assert.AreEqual(1, section.LeftLine);
        Assert.AreEqual(3, section.LeftCount);
        Assert.AreEqual(1, section.RightLine);
        Assert.AreEqual(3, section.RightCount);
    }

    // Git leaves out the count when it is 1, e.g. '@@ -1 +0,0 @@'. The missing count is then read
    // as 0 rather than 1.
    [TestMethod]
    public async Task TestParseSectionIndexesWithoutCount()
    {
        var file = FileOf(await GetCommitDiffAsync(ShowOutput), "gone.txt");
        var section = file.SectionDiffs[0];

        Assert.AreEqual("1 +0,0", section.ChangedIndexes);
        Assert.AreEqual(1, section.LeftLine);
        Assert.AreEqual(0, section.LeftCount);
        Assert.AreEqual(0, section.RightLine);
        Assert.AreEqual(0, section.RightCount);
    }

    // Tabs are expanded so the diff view can align columns, since it draws each line itself
    [TestMethod]
    public async Task TestParseExpandsTabs()
    {
        var output = """
            diff --git a/a.txt b/a.txt
            index 4cb29ea..ddc897f 100644
            --- a/a.txt
            +++ b/a.txt
            @@ -1,1 +1,1 @@
            -a\tb
            +c\td
            """.Replace("\\t", "\t");

        var file = (await GetCommitDiffAsync("commit 1\nAuthor: \nDate: \n\n \n\n" + output)).FileDiffs[0];

        Assert.AreEqual("DiffRemoved a   b\nDiffAdded c   d", LinesOf(file.SectionDiffs[0]));
    }

    // Git writes '\ No newline at end of file' between diff lines, which is not a line of the file
    [TestMethod]
    public async Task TestParseIgnoresNoNewlineMarker()
    {
        var output = """
            diff --git a/a.txt b/a.txt
            index 4cb29ea..ddc897f 100644
            --- a/a.txt
            +++ b/a.txt
            @@ -1,1 +1,1 @@
            -one
            \ No newline at end of file
            +two
            """;

        var file = (await GetCommitDiffAsync("commit 1\nAuthor: \nDate: \n\n \n\n" + output)).FileDiffs[0];

        Assert.AreEqual("DiffRemoved one\nDiffAdded two", LinesOf(file.SectionDiffs[0]));
    }

    // A UTF-8 BOM at the start of a file would otherwise be drawn as a stray rune
    [TestMethod]
    public async Task TestParseStripsByteOrderMark()
    {
        var output = """
            diff --git a/a.txt b/a.txt
            index 4cb29ea..ddc897f 100644
            --- a/a.txt
            +++ b/a.txt
            @@ -1,1 +1,1 @@
            +﻿one
            """.Replace("\\uFEFF", "﻿");

        var file = (await GetCommitDiffAsync("commit 1\nAuthor: \nDate: \n\n \n\n" + output)).FileDiffs[0];

        Assert.AreEqual("DiffAdded one", LinesOf(file.SectionDiffs[0]));
    }

    // Conflict markers are recognized so the diff view can split the file into the two sides.
    // Note the marker text loses one character (AsConflictLine trims two), which is invisible since
    // Cui/Diff/DiffService replaces the marker line with '=== Start of conflict'.
    [TestMethod]
    public async Task TestParseConflictMarkers()
    {
        var service = new DiffService(new FakeCmd(ConflictOutput));
        Assert.IsTrue(Try(out var commitDiff, out var e, await service.GetUncommittedDiff(TempWd())), $"{e}");

        var file = FileOf(commitDiff, "addboth.txt");
        Assert.AreEqual(DiffMode.DiffConflicts, file.DiffMode, "A file with markers is a conflict");
        Assert.AreEqual(
            """
            DiffConflictStart <<<<<< HEAD
            DiffSame mainboth
            DiffConflictSplit ======
            DiffAdded newboth
            DiffConflictEnd >>>>>> topic
            """,
            LinesOf(file.SectionDiffs[0])
        );
    }

    // A file without markers in the same diff is not marked as conflicted
    [TestMethod]
    public async Task TestParseFileWithoutMarkersInConflictedDiff()
    {
        var service = new DiffService(new FakeCmd(ConflictOutput));
        Assert.IsTrue(Try(out var commitDiff, out var e, await service.GetUncommittedDiff(TempWd())), $"{e}");

        Assert.AreEqual(DiffMode.DiffAdded, FileOf(commitDiff, "mod-del.txt").DiffMode);
    }

    // A combined diff is recognized as a conflict, but its '@@@ … @@@' hunk headers are not, so it
    // parses with no content. Nothing reaches this today, since no gmd git command asks for --cc.
    [TestMethod]
    public async Task TestParseCombinedDiffHasNoSections()
    {
        var commitDiff = await GetCommitDiffAsync(CombinedOutput);

        Assert.AreEqual(1, commitDiff.FileDiffs.Count);
        var file = commitDiff.FileDiffs[0];
        Assert.AreEqual("both.txt", file.PathAfter);
        Assert.AreEqual(DiffMode.DiffConflicts, file.DiffMode);
        Assert.AreEqual(0, file.SectionDiffs.Count);
    }

    // The 'Merge: <sha> <sha>' line of a merge commit is skipped, not read as the author
    [TestMethod]
    public async Task TestParseMergeCommitHeader()
    {
        var commitDiff = await GetCommitDiffAsync(CombinedOutput);

        Assert.AreEqual("1c6014174680fc2302e29dc5874a4ce5d39281fd", commitDiff.Id);
        Assert.AreEqual("Test <t@t.com>", commitDiff.Author);
        Assert.AreEqual("Merge branch 'topic'", commitDiff.Message);
    }

    // The file history is several commits in one output, newest first
    [TestMethod]
    public async Task TestParseFileDiffOfSeveralCommits()
    {
        var service = new DiffService(new FakeCmd(FileLogOutput));
        Assert.IsTrue(Try(out var commitDiffs, out var e, await service.GetFileDiffAsync("a.txt", "/wd")), $"{e}");

        Assert.AreEqual(2, commitDiffs.Length);
        Assert.AreEqual("Second commit with all kinds of change", commitDiffs[0].Message);
        Assert.AreEqual("Initial commit", commitDiffs[1].Message);
        Assert.AreEqual(DiffMode.DiffModified, commitDiffs[0].FileDiffs[0].DiffMode);
        Assert.AreEqual(DiffMode.DiffAdded, commitDiffs[1].FileDiffs[0].DiffMode);
    }

    [TestMethod]
    public async Task TestOutputWithoutCommitLineIsError()
    {
        var service = new DiffService(new FakeCmd("not a diff"));

        var result = await service.GetCommitDiffAsync("HEAD", "/wd");

        Assert.IsFalse(Try(out var _, out var _, result), "Expected a parse error");
    }

    [TestMethod]
    public async Task TestGitCommandFailureIsPropagated()
    {
        var service = new DiffService(new FakeCmd((_, _, _) => FakeCmd.Fail("fatal: bad object")));

        var result = await service.GetCommitDiffAsync("HEAD", "/wd");

        Assert.IsFalse(Try(out var _, out var _, result), "Expected the git failure to propagate");
    }

    [TestMethod]
    public async Task TestGetCommitDiffPassesArgsAndWorkingDirectoryToGit()
    {
        var cmd = new FakeCmd(ShowOutput);
        var service = new DiffService(cmd);

        await service.GetCommitDiffAsync("abc123", "/some/wd");

        Assert.AreEqual(1, cmd.Calls.Count);
        Assert.AreEqual("/some/wd", cmd.Calls[0].WorkingDirectory);
        StringAssert.StartsWith(cmd.Calls[0].Args, "show ");
        StringAssert.Contains(cmd.Calls[0].Args, "--find-renames --unified=6 abc123");
    }

    // A range diff has no commit header of its own, so it gets the message it was asked for
    [TestMethod]
    public async Task TestGetDiffRangeUsesGivenMessage()
    {
        var cmd = new FakeCmd(ConflictOutput);
        var service = new DiffService(cmd);

        Assert.IsTrue(Try(out var commitDiff, out var e, await service.GetDiffRangeAsync("a", "b", "Range", "/wd")));
        Assert.AreEqual("Range", commitDiff.Message);
        Assert.AreEqual("", commitDiff.Id);
        Assert.AreEqual(2, commitDiff.FileDiffs.Count);
        Assert.AreEqual("diff --find-renames --unified=6 --full-index a~..b", cmd.Calls[0].Args);
    }

    [TestMethod]
    public async Task TestGetRefsDiffUsesGivenMessage()
    {
        var cmd = new FakeCmd(ConflictOutput);
        var service = new DiffService(cmd);

        Assert.IsTrue(Try(out var commitDiff, out var e, await service.GetRefsDiffAsync("a", "b", "Refs", "/wd")));
        Assert.AreEqual("Refs", commitDiff.Message);
        Assert.AreEqual("diff --find-renames --unified=6 --full-index a b", cmd.Calls[0].Args);
    }

    [TestMethod]
    public async Task TestGetStashDiffUsesStashName()
    {
        var cmd = new FakeCmd(ConflictOutput);
        var service = new DiffService(cmd);

        Assert.IsTrue(Try(out var commitDiff, out var e, await service.GetStashDiffAsync("stash@{0}", "/wd")));
        Assert.AreEqual("Diff of stash stash@{0}", commitDiff.Message);
        StringAssert.StartsWith(cmd.Calls[0].Args, "stash show -u ");
    }

    // The uncommitted diff stages everything first, so renamed and added files are included, and
    // resets the staging afterwards
    [TestMethod]
    public async Task TestUncommittedDiffStagesAndResets()
    {
        var cmd = new FakeCmd(ConflictOutput);
        var service = new DiffService(cmd);

        Assert.IsTrue(Try(out var commitDiff, out var e, await service.GetUncommittedDiff(TempWd())), $"{e}");

        Assert.AreEqual("Uncommitted changes", commitDiff.Message);
        CollectionAssert.AreEqual(
            new[]
            {
                "add .",
                "diff --date=iso --first-parent --root --patch --no-color --find-renames --unified=6 HEAD",
                "reset",
            },
            cmd.Calls.Select(c => c.Args).ToArray()
        );
    }

    // While a merge is in progress the staging must be left alone, since staging is what marks a
    // conflict as resolved
    [TestMethod]
    public async Task TestUncommittedDiffDoesNotStageWhileMerging()
    {
        var wd = TempWd();
        File.WriteAllText(Path.Join(wd, ".git", "MERGE_MSG"), "Merge branch 'topic'\n");
        var cmd = new FakeCmd(ConflictOutput);
        var service = new DiffService(cmd);

        Assert.IsTrue(Try(out var _, out var e, await service.GetUncommittedDiff(wd)), $"{e}");

        Assert.AreEqual(1, cmd.Calls.Count, "Only the diff itself is run");
        StringAssert.StartsWith(cmd.Calls[0].Args, "diff --date=iso");
    }

    // In an empty repo there is no HEAD to diff against, so the staged diff is used instead
    [TestMethod]
    public async Task TestUncommittedDiffFallsBackToStagedInEmptyRepo()
    {
        var cmd = new FakeCmd(
            (_, args, _) =>
                args.EndsWith("HEAD")
                    ? FakeCmd.Fail("fatal: ambiguous argument 'HEAD': unknown revision")
                    : FakeCmd.Ok(ConflictOutput)
        );
        var service = new DiffService(cmd);

        Assert.IsTrue(Try(out var commitDiff, out var e, await service.GetUncommittedDiff(TempWd())), $"{e}");

        Assert.AreEqual(2, commitDiff.FileDiffs.Count);
        CollectionAssert.Contains(cmd.Calls.Select(c => c.Args).ToArray(), "diff --staged");
        CollectionAssert.Contains(cmd.Calls.Select(c => c.Args).ToArray(), "reset", "The staging is still reset");
    }

    // A failing diff must still undo the 'git add .' it did first
    [TestMethod]
    public async Task TestUncommittedDiffResetsWhenDiffFails()
    {
        var cmd = new FakeCmd(
            (_, args, _) => args.StartsWith("diff") ? FakeCmd.Fail("fatal: bad object") : FakeCmd.Ok("")
        );
        var service = new DiffService(cmd);

        var result = await service.GetUncommittedDiff(TempWd());

        Assert.IsFalse(Try(out var _, out var _, result), "Expected the git failure to propagate");
        Assert.AreEqual("add, diff, reset", string.Join(", ", cmd.Calls.Select(c => c.Args.Split(' ')[0])));
    }

    // GetUncommittedDiff reads .git/MERGE_MSG, so it needs a working directory. A plain temp folder
    // with a .git in it is enough, no repository required.
    static string TempWd()
    {
        var wd = Path.Join(Path.GetTempPath(), $"gmdTest-diff-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Join(wd, ".git"));
        tempDirs.Add(wd);
        return wd;
    }

    static readonly List<string> tempDirs = [];

    [ClassCleanup]
    public static void CleanupTempDirs()
    {
        tempDirs.Where(Directory.Exists).ToList().ForEach(d => Directory.Delete(d, true));
        tempDirs.Clear();
    }
}
