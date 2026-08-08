using gmd.Git;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Git;

// A thin layer of tests that run the real git executable against a throwaway repository, which
// is what the FakeCmd tests cannot do: they are the canary for git version and output format
// drift, since nothing here is canned. They are deliberately few and small, one per round trip.
//
// The tests are in their own category, so they can be excluded when only the fast tests are
// wanted:  ./test --filter "TestCategory!=Integration"
[TestClass]
[TestCategory("Integration")]
public class GitIntegrationTest
{
    TempRepo repo = null!;

    [TestInitialize]
    public async Task Init() => repo = await TempRepo.CreateAsync();

    [TestCleanup]
    public void Cleanup() => repo.Dispose();

    [TestMethod]
    public async Task TestVersion()
    {
        var version = Value(await repo.Git.Version());

        // The 'git version ' prefix is trimmed off, leaving e.g. '2.55.0'
        StringAssert.Matches(version, new System.Text.RegularExpressions.Regex(@"^\d+\.\d+"));
    }

    [TestMethod]
    public async Task TestRootPathIsFoundFromASubFolder()
    {
        var subFolder = Path.Join(repo.Path, "sub", "folder");
        Directory.CreateDirectory(subFolder);
        await repo.CommitFileAsync("file.txt", "text\n", "Initial");

        Assert.AreEqual(repo.Path, Value(repo.Git.RootPath(subFolder)));
    }

    [TestMethod]
    public async Task TestLogRoundTrip()
    {
        var c1 = await repo.CommitFileAsync("file.txt", "one\n", "First");
        var c2 = await repo.CommitFileAsync("file.txt", "two\n", "Second");

        var log = Value(await repo.Git.GetLogAsync(100, repo.Path));

        // Newest first, i.e. the order 'git log --date-order' returns
        Assert.AreEqual(2, log.Count);
        Assert.AreEqual("Second", log[0].Subject);
        Assert.AreEqual("First", log[1].Subject);

        Assert.AreEqual(c2, log[0].Id);
        Assert.AreEqual(c2.Sid(), log[0].Sid);
        CollectionAssert.AreEqual(new[] { c1 }, log[0].ParentIds);
        CollectionAssert.AreEqual(Array.Empty<string>(), log[1].ParentIds, "The first commit is a root commit");

        // The subject is the first line of the message, both come from the same '%B'
        Assert.AreEqual("Second", log[0].Message);
        Assert.AreEqual("Test User", log[0].Author);

        // Times are parsed from '%ai'/'%ci' into local time, so they are within minutes of now
        Assert.IsTrue(
            (DateTime.Now - log[0].AuthorTime).Duration() < TimeSpan.FromMinutes(10),
            $"Author time {log[0].AuthorTime} is not close to now"
        );
        Assert.AreEqual(log[0].AuthorTime, log[0].CommitTime);
    }

    [TestMethod]
    public async Task TestLogMaxCount()
    {
        await repo.CommitFileAsync("file.txt", "one\n", "First");
        await repo.CommitFileAsync("file.txt", "two\n", "Second");
        await repo.CommitFileAsync("file.txt", "three\n", "Third");

        var log = Value(await repo.Git.GetLogAsync(2, repo.Path));

        Assert.AreEqual(2, log.Count);
        Assert.AreEqual("Third", log[0].Subject);
    }

    [TestMethod]
    public async Task TestBranchesRoundTrip()
    {
        var c1 = await repo.CommitFileAsync("file.txt", "one\n", "Initial");
        Ok(await repo.Git.CreateBranchAsync("dev", true, repo.Path));
        var d1 = await repo.CommitFileAsync("dev.txt", "dev\n", "Dev work");

        var branches = Value(await repo.Git.GetBranchesAsync(repo.Path));

        Assert.AreEqual("dev, main", string.Join(", ", branches.Select(b => b.Name)));

        var dev = branches.First(b => b.Name == "dev");
        Assert.AreEqual(d1, dev.TipID);
        Assert.IsTrue(dev.IsCurrent);
        Assert.IsFalse(dev.IsRemote);
        Assert.IsFalse(dev.IsDetached);
        Assert.AreEqual("", dev.RemoteName, "No remote, so no branch is tracking one");

        var main = branches.First(b => b.Name == "main");
        Assert.AreEqual(c1, main.TipID);
        Assert.IsFalse(main.IsCurrent);
    }

    // Renaming a branch the user is not on leaves them where they are, which is the reason gmd
    // renames with 'git branch -m' rather than creating a branch and deleting the old one
    [TestMethod]
    public async Task TestRenameBranchLeavesTheCurrentBranchAlone()
    {
        var c1 = await repo.CommitFileAsync("file.txt", "one\n", "Initial");
        Ok(await repo.Git.CreateBranchAsync("dev", false, repo.Path));

        Ok(await repo.Git.RenameBranchAsync("dev", "dev2", repo.Path));

        var branches = Value(await repo.Git.GetBranchesAsync(repo.Path));
        Assert.AreEqual("dev2, main", string.Join(", ", branches.Select(b => b.Name)));
        Assert.AreEqual(c1, branches.First(b => b.Name == "dev2").TipID, "The branch still points at its commit");
        Assert.IsTrue(branches.First(b => b.Name == "main").IsCurrent, "Still on the branch we were on");
    }

    // Git moves HEAD along when the renamed branch is the current one, so no checkout is needed
    [TestMethod]
    public async Task TestRenameCurrentBranchMovesHead()
    {
        await repo.CommitFileAsync("file.txt", "one\n", "Initial");

        Ok(await repo.Git.RenameBranchAsync("main", "main2", repo.Path));

        Assert.AreEqual("main2", await repo.GitAsync("rev-parse --abbrev-ref HEAD"));
        var branches = Value(await repo.Git.GetBranchesAsync(repo.Path));
        Assert.IsTrue(branches.First(b => b.Name == "main2").IsCurrent);
    }

    // Renaming the remote half is a push of the new name followed by a delete of the old one, and
    // the push is also the repair: 'git branch -m' renames the branch but leaves it tracking the
    // old remote branch, and PushBranchAsync pushes an explicit refspec with --set-upstream, which
    // is what moves the tracking on to the new remote branch. Without that a later pull or push of
    // the renamed branch would still go to the old name.
    [TestMethod]
    public async Task TestRenameBranchAndItsRemoteBranch()
    {
        await repo.CommitFileAsync("file.txt", "one\n", "Initial");
        await repo.AddOriginAsync();
        Ok(await repo.Git.CreateBranchAsync("dev", true, repo.Path));
        Ok(await repo.Git.PushBranchAsync("dev", repo.Path));

        Ok(await repo.Git.RenameBranchAsync("dev", "dev2", repo.Path));
        StringAssert.Contains(
            await repo.GitAsync("branch -vv --no-color"),
            "[origin/dev]",
            "The rename alone leaves the branch tracking the old remote branch"
        );

        Ok(await repo.Git.PushBranchAsync("dev2", repo.Path));
        Ok(await repo.Git.DeleteRemoteBranchAsync("dev", repo.Path));

        var branches = Value(await repo.Git.GetBranchesAsync(repo.Path));
        Assert.AreEqual("dev2, main, origin/dev2", string.Join(", ", branches.Select(b => b.Name).Order()));
        Assert.AreEqual("origin/dev2", branches.First(b => b.Name == "dev2").RemoteName, "Tracks the new name");
    }

    // '-m' and not '-M', so an existing branch is never silently overwritten
    [TestMethod]
    public async Task TestRenameToAnExistingBranchNameFails()
    {
        await repo.CommitFileAsync("file.txt", "one\n", "Initial");
        Ok(await repo.Git.CreateBranchAsync("dev", false, repo.Path));

        var result = await repo.Git.RenameBranchAsync("dev", "main", repo.Path);

        Assert.IsFalse(Try(out var _, result), "Expected the rename to be refused");
        var branches = Value(await repo.Git.GetBranchesAsync(repo.Path));
        Assert.AreEqual("dev, main", string.Join(", ", branches.Select(b => b.Name)));
    }

    [TestMethod]
    public async Task TestDetachedHead()
    {
        var c1 = await repo.CommitFileAsync("file.txt", "one\n", "First");
        await repo.CommitFileAsync("file.txt", "two\n", "Second");
        Ok(await repo.Git.CheckoutAsync(c1, repo.Path));

        var branches = Value(await repo.Git.GetBranchesAsync(repo.Path));

        // Git writes '(HEAD detached at <ref>)', which all become the one DETACHED branch
        var detached = branches.First(b => b.IsDetached);
        Assert.AreEqual("DETACHED", detached.Name);
        Assert.AreEqual(c1, detached.TipID);
        Assert.IsTrue(detached.IsCurrent);
        Assert.IsFalse(branches.First(b => b.Name == "main").IsCurrent);
    }

    // Ahead/behind are read from the '[origin/main: ahead 1, behind 1]' part of 'git branch -vv',
    // which needs a real remote to be written at all
    [TestMethod]
    public async Task TestAheadBehindOfATrackingBranch()
    {
        var c1 = await repo.CommitFileAsync("file.txt", "one\n", "First");
        await repo.AddOriginAsync();
        Ok(await repo.Git.PushBranchAsync("main", repo.Path));

        var branches = Value(await repo.Git.GetBranchesAsync(repo.Path));
        Assert.AreEqual("main, origin/main", string.Join(", ", branches.Select(b => b.Name)));

        var main = branches.First(b => b.Name == "main");
        Assert.AreEqual("origin/main", main.RemoteName, "Pushing with --set-upstream makes main track origin/main");
        Assert.AreEqual(0, main.AheadCount);
        Assert.AreEqual(0, main.BehindCount);

        var origin = branches.First(b => b.Name == "origin/main");
        Assert.IsTrue(origin.IsRemote);
        Assert.AreEqual("", origin.RemoteName);
        Assert.AreEqual(main.TipID, origin.TipID);

        // Push a commit and then drop it locally, so the local branch is behind its remote
        await repo.CommitFileAsync("file.txt", "two\n", "Second");
        Ok(await repo.Git.PushBranchAsync("main", repo.Path));
        await repo.GitAsync($"reset --hard {c1}");

        main = Value(await repo.Git.GetBranchesAsync(repo.Path)).First(b => b.Name == "main");
        Assert.AreEqual(0, main.AheadCount);
        Assert.AreEqual(1, main.BehindCount);

        // A local commit on top makes the pair diverged, i.e. both ahead and behind
        await repo.CommitFileAsync("file.txt", "three\n", "Third");

        branches = Value(await repo.Git.GetBranchesAsync(repo.Path));
        main = branches.First(b => b.Name == "main");
        Assert.AreEqual(1, main.AheadCount);
        Assert.AreEqual(1, main.BehindCount);
        Assert.AreNotEqual(main.TipID, branches.First(b => b.Name == "origin/main").TipID);
    }

    [TestMethod]
    public async Task TestStatusRoundTrip()
    {
        await repo.CommitFileAsync("mod.txt", "one\n", "First");
        await repo.CommitFileAsync("del.txt", "two\n", "Second");

        var status = Value(await repo.Git.GetStatusAsync(repo.Path));
        Assert.AreEqual("M:0,A:0,D:0,C:0,R:0", status.ToString(), "A just committed repo is clean");
        Assert.IsFalse(status.IsMerging);

        repo.WriteFile("mod.txt", "one\nchanged\n");
        repo.DeleteFile("del.txt");
        repo.WriteFile("new.txt", "new\n");

        status = Value(await repo.Git.GetStatusAsync(repo.Path));

        Assert.AreEqual("M:1,A:1,D:1,C:0,R:0", status.ToString());
        CollectionAssert.AreEqual(new[] { "mod.txt" }, status.ModifiedFiles);
        CollectionAssert.AreEqual(new[] { "new.txt" }, status.AddedFiles, "An untracked file is an added file");
        CollectionAssert.AreEqual(new[] { "del.txt" }, status.DeletedFiles);
    }

    [TestMethod]
    public async Task TestMergeRoundTrip()
    {
        await repo.CommitFileAsync("file.txt", "one\n", "Initial");
        Ok(await repo.Git.CreateBranchAsync("dev", true, repo.Path));
        var d1 = await repo.CommitFileAsync("dev.txt", "dev\n", "Dev work");
        Ok(await repo.Git.CheckoutAsync("main", repo.Path));
        var c2 = await repo.CommitFileAsync("main.txt", "main\n", "Main work");

        // Merge is '--no-ff --no-commit', so the merge is left staged for the user to commit
        Ok(await repo.Git.MergeBranchAsync("dev", repo.Path));

        var status = Value(await repo.Git.GetStatusAsync(repo.Path));
        Assert.IsTrue(status.IsMerging, "Read from .git/MERGE_HEAD and .git/MERGE_MSG");
        Assert.AreEqual("Merge branch 'dev'", status.MergeMessage);
        Assert.AreEqual(d1, status.MergeHeadId);

        // The merged in file is staged as an add, which is counted as modified. See the Step 4
        // finding in MODERNIZATION.md, this pins that the counts are visibly the ones described.
        Assert.AreEqual("M:1,A:0,D:0,C:0,R:0", status.ToString());
        CollectionAssert.AreEqual(new[] { "dev.txt" }, status.ModifiedFiles);

        // Committing the merge is what the commit dialog does with the merge message prefilled
        var mergeId = await repo.CommitAsync(status.MergeMessage);

        var log = Value(await repo.Git.GetLogAsync(100, repo.Path));
        Assert.AreEqual(mergeId, log[0].Id);
        Assert.AreEqual("Merge branch 'dev'", log[0].Subject);
        CollectionAssert.AreEqual(new[] { c2, d1 }, log[0].ParentIds, "Merged into first, merged from second");

        // The branch name of a merged in branch is only recorded in the merge subject, so the
        // subject git writes has to stay one that BranchNameService can read
        var fromInto = new BranchNameService().ParseSubject(log[0].Subject);
        Assert.AreEqual("dev", fromInto.From);
        Assert.AreEqual("", fromInto.Into, "Git omits 'into <branch>' when merging into 'main' or 'master'");
    }

    // The form gmd's inference relies on most: a merge into anything but 'main'/'master' names
    // both branches, which is the only record of which branch the merge commit itself is on
    [TestMethod]
    public async Task TestMergeIntoANonDefaultBranchNamesBothBranches()
    {
        await repo.CommitFileAsync("file.txt", "one\n", "Initial");
        Ok(await repo.Git.CreateBranchAsync("feature", true, repo.Path));
        await repo.CommitFileAsync("feature.txt", "feature\n", "Feature work");
        Ok(await repo.Git.CreateBranchAsync("dev", true, repo.Path));
        await repo.CommitFileAsync("dev.txt", "dev\n", "Dev work");
        Ok(await repo.Git.CheckoutAsync("feature", repo.Path));
        Ok(await repo.Git.MergeBranchAsync("dev", repo.Path));

        var status = Value(await repo.Git.GetStatusAsync(repo.Path));
        Assert.AreEqual("Merge branch 'dev' into feature", status.MergeMessage);

        var fromInto = new BranchNameService().ParseSubject(status.MergeMessage);
        Assert.AreEqual("dev", fromInto.From);
        Assert.AreEqual("feature", fromInto.Into);
    }

    [TestMethod]
    public async Task TestMergeWithConflicts()
    {
        await repo.CommitFileAsync("file.txt", "one\n", "Initial");
        Ok(await repo.Git.CreateBranchAsync("dev", true, repo.Path));
        var d1 = await repo.CommitFileAsync("file.txt", "dev\n", "Dev work");
        Ok(await repo.Git.CheckoutAsync("main", repo.Path));
        await repo.CommitFileAsync("file.txt", "main\n", "Main work");

        var result = await repo.Git.MergeBranchAsync("dev", repo.Path);

        Assert.IsTrue(result.IsResultError, "Both branches changed the same line");
        StringAssert.Contains(result.GetResultError().ErrorMessage, "Merge Conflicts!");

        var status = Value(await repo.Git.GetStatusAsync(repo.Path));
        Assert.AreEqual("M:0,A:0,D:0,C:1,R:0", status.ToString());
        CollectionAssert.AreEqual(new[] { "file.txt" }, status.ConflictsFiles);
        Assert.IsTrue(status.IsMerging);
        Assert.AreEqual(d1, status.MergeHeadId);

        // The conflict markers git wrote into the file are what the diff view draws as conflicts
        var diff = Value(await repo.Git.GetUncommittedDiff(6, repo.Path));
        var fileDiff = diff.FileDiffs.First(f => f.PathAfter == "file.txt");
        Assert.AreEqual(DiffMode.DiffConflicts, fileDiff.DiffMode);
        Assert.AreEqual(
            "DiffConflictStart, DiffSame, DiffConflictSplit, DiffAdded, DiffConflictEnd",
            string.Join(", ", fileDiff.SectionDiffs[0].LineDiffs.Select(l => l.DiffMode))
        );
    }

    [TestMethod]
    public async Task TestCommitDiffRoundTrip()
    {
        await repo.CommitFileAsync("file.txt", "one\ntwo\nthree\n", "First");
        var c2 = await repo.CommitFileAsync("file.txt", "one\nchanged\nthree\n", "Second");

        var diff = Value(await repo.Git.GetCommitDiffAsync(c2, 6, repo.Path));

        Assert.AreEqual(c2, diff.Id);
        Assert.AreEqual("Test User <test@example.com>", diff.Author);
        Assert.AreEqual("Second", diff.Message);

        var fileDiff = diff.FileDiffs.Single();
        Assert.AreEqual("file.txt", fileDiff.PathBefore);
        Assert.AreEqual("file.txt", fileDiff.PathAfter);
        Assert.AreEqual(DiffMode.DiffModified, fileDiff.DiffMode);
        Assert.IsFalse(fileDiff.IsRenamed);
        Assert.IsFalse(fileDiff.IsBinary);

        var section = fileDiff.SectionDiffs.Single();
        Assert.AreEqual(1, section.LeftLine);
        Assert.AreEqual(3, section.LeftCount);
        Assert.AreEqual(1, section.RightLine);
        Assert.AreEqual(3, section.RightCount);
        Assert.AreEqual(
            "DiffSame 'one', DiffRemoved 'two', DiffAdded 'changed', DiffSame 'three'",
            string.Join(", ", section.LineDiffs.Select(l => $"{l.DiffMode} '{l.Line}'"))
        );
    }

    // What the diff view's '+' and '-' keys actually buy: git shows more unchanged lines around
    // the change, and a context larger than the file stops at its ends rather than failing, which
    // is how 'whole file' is asked for. Canned output cannot catch this one — it is git's own
    // behavior, and the value used for the whole file is well outside what any test fixture has.
    [TestMethod]
    public async Task TestMoreContextShowsMoreOfTheFile()
    {
        var lines = Enumerable.Range(1, 60).Select(i => $"line {i}").ToList();
        await repo.CommitFileAsync("file.txt", string.Join("\n", lines) + "\n", "First");

        lines[29] = "line 30 changed";
        var c2 = await repo.CommitFileAsync("file.txt", string.Join("\n", lines) + "\n", "Second");

        // 6 and 15 lines either side of the one changed line, then all 59 lines that did not change
        Assert.AreEqual(12, await SameLineCountAsync(6));
        Assert.AreEqual(30, await SameLineCountAsync(15));
        Assert.AreEqual(
            59,
            await SameLineCountAsync(gmd.Cui.Diff.DiffContext.WholeFile),
            "The rest of the 60 line file"
        );

        async Task<int> SameLineCountAsync(int contextLines)
        {
            var diff = Value(await repo.Git.GetCommitDiffAsync(c2, contextLines, repo.Path));

            return diff
                .FileDiffs.Single()
                .SectionDiffs.Sum(s => s.LineDiffs.Count(l => l.DiffMode == DiffMode.DiffSame));
        }
    }

    // GetUncommittedDiff stages the changes, diffs them and resets the index again, since a diff
    // of untracked files is not otherwise possible. This pins that the working folder is left as
    // it was, which the FakeCmd tests can only assert the git commands for.
    [TestMethod]
    public async Task TestUncommittedDiffLeavesTheStatusUnchanged()
    {
        await repo.CommitFileAsync("mod.txt", "one\n", "First");
        await repo.CommitFileAsync("del.txt", "two\n", "Second");

        repo.WriteFile("mod.txt", "one\nchanged\n");
        repo.DeleteFile("del.txt");
        repo.WriteFile("new.txt", "new\n");

        var diff = Value(await repo.Git.GetUncommittedDiff(6, repo.Path));

        Assert.AreEqual("Uncommitted changes", diff.Message);
        Assert.AreEqual(
            "del.txt DiffRemoved, mod.txt DiffModified, new.txt DiffAdded",
            string.Join(", ", diff.FileDiffs.Select(f => $"{f.PathAfter} {f.DiffMode}"))
        );

        var status = Value(await repo.Git.GetStatusAsync(repo.Path));
        Assert.AreEqual("M:1,A:1,D:1,C:0,R:0", status.ToString(), "The staged changes were reset again");
    }

    // Unwraps a result, failing the test with the git error if the command failed
    static T Value<T>(R<T> result)
    {
        Assert.IsTrue(Try(out var value, out var e, result), $"Git failed: {e}");
        return value;
    }

    static void Ok(R result) => Assert.IsTrue(Try(out var e, result), $"Git failed: {e}");
}
