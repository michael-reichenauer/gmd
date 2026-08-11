using gmd.Git;
using gmd.Git.Private;
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

    // Sets up 'main' and 'dev' both changing the same line, so anything replaying one onto the
    // other conflicts. Returns the dev commit id.
    async Task<string> TwoBranchesThatConflictAsync()
    {
        await repo.CommitFileAsync("file.txt", "one\n", "Initial");
        Ok(await repo.Git.CreateBranchAsync("dev", true, repo.Path));
        var d1 = await repo.CommitFileAsync("file.txt", "dev\n", "Dev work");
        Ok(await repo.Git.CheckoutAsync("main", repo.Path));
        await repo.CommitFileAsync("file.txt", "main\n", "Main work");
        return d1;
    }

    // The regression test for silent, unrecoverable data loss. A rebase with the --apply backend
    // writes no .git/MERGE_MSG, which is what the merge-in-progress check used to look for, so
    // GetUncommittedDiff ran its 'git add .' — staging the unmerged path with the conflict markers
    // as its content. That resolves the conflict, and the 'git reset' afterwards does not put the
    // stages back: 'git checkout --merge' can no longer recover it, only 'git rebase --abort'.
    //
    // Merely opening the diff view was enough to trigger it.
    [TestMethod]
    public async Task TestDiffDuringAnApplyRebaseKeepsTheConflictUnmerged()
    {
        await TwoBranchesThatConflictAsync();
        Ok(await repo.Git.CheckoutAsync("dev", repo.Path));
        await repo.GitAllowFailAsync("rebase --apply main");

        var before = Value(await repo.Git.GetStatusAsync(repo.Path));
        Assert.AreEqual(GitOperation.Rebase, before.Operation, "The --apply backend is still a rebase");
        Assert.IsFalse(File.Exists(Path.Join(repo.Path, ".git", "MERGE_MSG")), "Which is why it was missed");

        Value(await repo.Git.GetUncommittedDiff(6, repo.Path));

        var unmerged = await repo.GitAsync("ls-files -u");
        Assert.AreNotEqual("", unmerged.Trim(), "The index stages must survive being looked at");
        var after = Value(await repo.Git.GetStatusAsync(repo.Path));
        Assert.AreEqual(1, after.Conflicted, "Still one conflict, not resolved behind the user's back");
    }

    // The same for a merge, which was already safe — it is here so that the guard cannot regress
    // for the case that did work
    [TestMethod]
    public async Task TestDiffDuringAMergeKeepsTheConflictUnmerged()
    {
        await TwoBranchesThatConflictAsync();
        await repo.Git.MergeBranchAsync("dev", repo.Path);

        Value(await repo.Git.GetUncommittedDiff(6, repo.Path));

        Assert.AreNotEqual("", (await repo.GitAsync("ls-files -u")).Trim());
    }

    // 'git commit -am' during a conflicted merge succeeds and commits the '<<<<<<<' markers into
    // history — the reason the commit path stages nothing while an operation is in progress
    [TestMethod]
    public async Task TestCommitDuringAMergeDoesNotCommitConflictMarkers()
    {
        await TwoBranchesThatConflictAsync();
        await repo.Git.MergeBranchAsync("dev", repo.Path);

        var headBefore = await repo.HeadIdAsync();

        var result = await repo.Git.CommitAllChangesAsync("Merge branch 'dev'", false, repo.Path);

        Assert.IsTrue(result.IsResultError, "Git refuses to commit while a path is unmerged");
        StringAssert.Contains(result.GetResultError().ErrorMessage, "unresolved conflicts");
        Assert.AreEqual(headBefore, await repo.HeadIdAsync(), "No commit was made");
    }

    // Resolving in an external editor and not staging is a real workflow, and it used to work by
    // accident: 'git commit -am' staged the edited file for the user. Now that nothing is staged
    // git refuses, so the message has to say what is missing rather than passing on git's hints.
    [TestMethod]
    public async Task TestCommitAfterAHandResolveSaysWhatIsMissing()
    {
        await TwoBranchesThatConflictAsync();
        await repo.Git.MergeBranchAsync("dev", repo.Path);
        repo.WriteFile("file.txt", "resolved by hand\n");

        var result = await repo.Git.CommitAllChangesAsync("Merge branch 'dev'", false, repo.Path);

        Assert.IsTrue(result.IsResultError, "The path is still unmerged until it is staged");
        StringAssert.Contains(result.GetResultError().ErrorMessage, "mark it resolved");
    }

    // ... and once it is staged, which is what 'git mergetool' does for you, the commit goes through
    [TestMethod]
    public async Task TestCommitAfterAStagedResolveSucceeds()
    {
        await TwoBranchesThatConflictAsync();
        await repo.Git.MergeBranchAsync("dev", repo.Path);
        repo.WriteFile("file.txt", "resolved by hand\n");
        await repo.GitAsync("add file.txt");

        Ok(await repo.Git.CommitAllChangesAsync("Merge branch 'dev'", false, repo.Path));

        Assert.AreEqual("Merge branch 'dev'", (await repo.GitAsync("log -1 --pretty=%s")).Trim());
        var status = Value(await repo.Git.GetStatusAsync(repo.Path));
        Assert.AreEqual(GitOperation.None, status.Operation, "The merge is finished");
    }

    // gmd's own cherry-pick is 'cherry-pick --no-commit', and that writes no CHERRY_PICK_HEAD at
    // all — only MERGE_MSG. Reporting a merge is right rather than a shortcoming: with --no-commit
    // there is no cherry-pick sequence to continue or abort ('git cherry-pick --abort' answers
    // "no cherry-pick or revert in progress"), only a conflicted index to resolve and commit.
    [TestMethod]
    public async Task TestOperationOfGmdsOwnCherryPickIsAMerge()
    {
        var d1 = await TwoBranchesThatConflictAsync();
        await repo.GitAllowFailAsync($"cherry-pick --no-commit {d1}");

        var status = Value(await repo.Git.GetStatusAsync(repo.Path));

        Assert.IsFalse(File.Exists(Path.Join(repo.Path, ".git", "CHERRY_PICK_HEAD")));
        Assert.AreEqual(GitOperation.Merge, status.Operation);
        Assert.AreEqual(1, status.Conflicted);
        Assert.AreEqual(ConflictKind.BothModified, status.Conflicts[0].Kind);
    }

    // A cherry-pick started outside gmd, which does record a sequence to continue or abort
    [TestMethod]
    public async Task TestOperationIsDetectedForARealCherryPick()
    {
        var d1 = await TwoBranchesThatConflictAsync();
        await repo.GitAllowFailAsync($"cherry-pick {d1}");

        var status = Value(await repo.Git.GetStatusAsync(repo.Path));

        Assert.AreEqual(GitOperation.CherryPick, status.Operation);
        Assert.AreEqual(d1, status.MergeHeadId, "Read from CHERRY_PICK_HEAD");
    }

    [TestMethod]
    public async Task TestOperationIsDetectedForARealRebase()
    {
        await TwoBranchesThatConflictAsync();
        Ok(await repo.Git.CheckoutAsync("dev", repo.Path));
        await repo.GitAllowFailAsync("rebase main");

        var status = Value(await repo.Git.GetStatusAsync(repo.Path));

        Assert.AreEqual(GitOperation.Rebase, status.Operation);
        Assert.AreEqual("dev", status.OperationBranchName);
        Assert.AreEqual(1, status.OperationStep);
        Assert.AreEqual(1, status.OperationTotal);
    }

    // Abort puts the working folder back as it was, from each of the operations gmd can start
    [TestMethod]
    public async Task TestAbortOfAConflictedMerge()
    {
        await TwoBranchesThatConflictAsync();
        var head = await repo.HeadIdAsync();
        await repo.Git.MergeBranchAsync("dev", repo.Path);

        Ok(await repo.Git.AbortOperationAsync(repo.Path));

        var status = Value(await repo.Git.GetStatusAsync(repo.Path));
        Assert.AreEqual(GitOperation.None, status.Operation);
        Assert.AreEqual(0, status.Conflicted, "The working folder is clean again");
        Assert.AreEqual(head, await repo.HeadIdAsync());
    }

    [TestMethod]
    public async Task TestAbortOfAConflictedRebase()
    {
        await TwoBranchesThatConflictAsync();
        Ok(await repo.Git.CheckoutAsync("dev", repo.Path));
        var head = await repo.HeadIdAsync();
        await repo.GitAllowFailAsync("rebase main");

        Ok(await repo.Git.AbortOperationAsync(repo.Path));

        var status = Value(await repo.Git.GetStatusAsync(repo.Path));
        Assert.AreEqual(GitOperation.None, status.Operation);
        Assert.AreEqual(head, await repo.HeadIdAsync(), "Back on the commit it started from");
    }

    [TestMethod]
    public async Task TestAbortOfAConflictedCherryPick()
    {
        var d1 = await TwoBranchesThatConflictAsync();
        await repo.GitAllowFailAsync($"cherry-pick {d1}");

        Ok(await repo.Git.AbortOperationAsync(repo.Path));

        Assert.AreEqual(GitOperation.None, Value(await repo.Git.GetStatusAsync(repo.Path)).Operation);
    }

    // Continuing is what finishes a rebase, and gmd could not do it at all before: 'git commit'
    // makes the commit but leaves the rebase mid flight with its remaining commits unapplied
    [TestMethod]
    public async Task TestContinueFinishesAConflictedRebase()
    {
        await TwoBranchesThatConflictAsync();
        Ok(await repo.Git.CheckoutAsync("dev", repo.Path));
        await repo.GitAllowFailAsync("rebase main");
        repo.WriteFile("file.txt", "resolved\n");
        await repo.GitAsync("add file.txt");

        Ok(await repo.Git.ContinueOperationAsync(repo.Path));

        var status = Value(await repo.Git.GetStatusAsync(repo.Path));
        Assert.AreEqual(GitOperation.None, status.Operation, "The rebase is finished, not left mid flight");
        Assert.AreEqual(0, status.Conflicted);
        Assert.AreEqual("Dev work", (await repo.GitAsync("log -1 --pretty=%s")).Trim());
    }

    // The regression test for a hang, and it has to set a real blocking editor to be worth anything.
    //
    // 'git rebase --continue' opens an editor on the commit message, and GIT_EDITOR takes precedence
    // over core.editor — so passing '-c core.editor=true' looked like it prevented that and did
    // nothing at all for any user who has GIT_EDITOR set, which VS Code does ('code --wait'). gmd
    // owns the terminal, so the editor never appears and the application simply freezes.
    //
    // This was invisible in development because the shell used there happened to have
    // GIT_EDITOR=true already, i.e. the very thing being tested was supplied by the environment.
    [TestMethod]
    public async Task TestContinueDoesNotOpenAnEditorEvenWhenGitEditorIsSet()
    {
        if (OperatingSystem.IsWindows())
            Assert.Inconclusive("The blocking editor this needs is a shell script");

        await TwoBranchesThatConflictAsync();
        Ok(await repo.Git.CheckoutAsync("dev", repo.Path));
        await repo.GitAllowFailAsync("rebase main");
        repo.WriteFile("file.txt", "resolved\n");
        await repo.GitAsync("add file.txt");

        // An editor that never returns, which is what 'vi' or 'code --wait' is to a process with no
        // terminal of its own. Restored afterwards, since it is inherited by every later child.
        var before = Environment.GetEnvironmentVariable("GIT_EDITOR");
        Environment.SetEnvironmentVariable("GIT_EDITOR", BlockingEditor());
        try
        {
            var continued = repo.Git.ContinueOperationAsync(repo.Path);
            var finished = await Task.WhenAny(continued, Task.Delay(TimeSpan.FromSeconds(30)));

            Assert.AreSame(continued, finished, "'rebase --continue' hung waiting for an editor");
            Assert.IsTrue(Try(out var e, await continued), $"{e}");
        }
        finally
        {
            Environment.SetEnvironmentVariable("GIT_EDITOR", before);
        }

        Assert.AreEqual(GitOperation.None, Value(await repo.Git.GetStatusAsync(repo.Path)).Operation);
    }

    // A command that blocks for longer than the test would wait, written into the repo so it is
    // cleaned up with it
    string BlockingEditor()
    {
        var path = Path.Join(repo.Path, "blocking-editor.sh");
        File.WriteAllText(path, "#!/bin/sh\nexec sleep 300\n");
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        return path;
    }

    // The editor '--continue' would otherwise open would hang gmd behind the terminal it owns
    [TestMethod]
    public async Task TestContinueOfACherryPickDoesNotOpenAnEditor()
    {
        var d1 = await TwoBranchesThatConflictAsync();
        await repo.GitAllowFailAsync($"cherry-pick {d1}");
        repo.WriteFile("file.txt", "resolved\n");
        await repo.GitAsync("add file.txt");

        Ok(await repo.Git.ContinueOperationAsync(repo.Path));

        Assert.AreEqual("Dev work", (await repo.GitAsync("log -1 --pretty=%s")).Trim());
    }

    [TestMethod]
    public async Task TestContinueWithConflictsStillUnresolvedSaysWhatIsMissing()
    {
        await TwoBranchesThatConflictAsync();
        Ok(await repo.Git.CheckoutAsync("dev", repo.Path));
        await repo.GitAllowFailAsync("rebase main");

        var result = await repo.Git.ContinueOperationAsync(repo.Path);

        Assert.IsTrue(result.IsResultError);
        StringAssert.Contains(result.GetResultError().ErrorMessage, "unresolved conflicts");
    }

    // A rebase over two commits that conflicts twice: continuing gets past the first and stops on
    // the second, which is the operation working rather than failing
    [TestMethod]
    public async Task TestContinueThatStopsOnTheNextCommitSaysSo()
    {
        await repo.CommitFileAsync("file.txt", "a\nb\n", "Initial");
        Ok(await repo.Git.CreateBranchAsync("dev", true, repo.Path));
        await repo.CommitFileAsync("file.txt", "dev1\nb\n", "Dev 1");
        await repo.CommitFileAsync("file.txt", "dev1\ndev2\n", "Dev 2");
        Ok(await repo.Git.CheckoutAsync("main", repo.Path));
        await repo.CommitFileAsync("file.txt", "main1\nmain2\n", "Main work");
        Ok(await repo.Git.CheckoutAsync("dev", repo.Path));
        await repo.GitAllowFailAsync("rebase main");
        repo.WriteFile("file.txt", "r1\nmain2\n");
        await repo.GitAsync("add file.txt");

        var result = await repo.Git.ContinueOperationAsync(repo.Path);

        Assert.IsTrue(result.IsResultError);
        StringAssert.Contains(result.GetResultError().ErrorMessage, "stopped on more conflicts");
        var status = Value(await repo.Git.GetStatusAsync(repo.Path));
        Assert.AreEqual(GitOperation.Rebase, status.Operation, "Still rebasing, now on the second commit");
        Assert.AreEqual(2, status.OperationStep);
    }

    // Skipping drops the commit the rebase stopped on and carries on with the rest
    [TestMethod]
    public async Task TestSkipDropsTheCommitTheRebaseStoppedOn()
    {
        await TwoBranchesThatConflictAsync();
        Ok(await repo.Git.CheckoutAsync("dev", repo.Path));
        await repo.GitAllowFailAsync("rebase main");

        Ok(await repo.Git.SkipOperationAsync(repo.Path));

        var status = Value(await repo.Git.GetStatusAsync(repo.Path));
        Assert.AreEqual(GitOperation.None, status.Operation);
        Assert.AreEqual("Main work", (await repo.GitAsync("log -1 --pretty=%s")).Trim(), "'Dev work' was dropped");
    }

    // Marking a file resolved is just 'git add', and git does not look at what it stages — so a
    // file staged with the markers still in it commits '<<<<<<<' into history. Neither git nor the
    // guards on gmd's own staging catch it, because the user staged it deliberately.
    [TestMethod]
    public async Task TestLeftoverMarkersInAFileStagedAsResolvedAreFound()
    {
        await TwoBranchesThatConflictAsync();
        await repo.Git.MergeBranchAsync("dev", repo.Path);
        await repo.GitAsync("add file.txt"); // Marked resolved without removing the markers

        var paths = Value(await repo.Git.GetLeftoverMarkerPathsAsync(repo.Path));

        CollectionAssert.AreEqual(new[] { "file.txt" }, paths.ToArray());
    }

    [TestMethod]
    public async Task TestNoLeftoverMarkersOnceTheFileIsReallyResolved()
    {
        await TwoBranchesThatConflictAsync();
        await repo.Git.MergeBranchAsync("dev", repo.Path);
        repo.WriteFile("file.txt", "resolved\n");
        await repo.GitAsync("add file.txt");

        var paths = Value(await repo.Git.GetLeftoverMarkerPathsAsync(repo.Path));

        Assert.AreEqual(0, paths.Count);
    }

    // 'git diff --cached --check' also reports whitespace problems, and exits non-zero for them
    // too. Trusting the exit code would refuse an ordinary commit over a trailing space.
    [TestMethod]
    public async Task TestTrailingWhitespaceIsNotReportedAsAConflictMarker()
    {
        await repo.CommitFileAsync("file.txt", "one\n", "Initial");
        repo.WriteFile("file.txt", "one\ntwo   \n");
        await repo.GitAsync("add file.txt");

        var paths = Value(await repo.Git.GetLeftoverMarkerPathsAsync(repo.Path));

        Assert.AreEqual(0, paths.Count, "A trailing space must not block a commit");
    }

    // ---- reading and writing a conflicted file ----

    // Makes 'file.txt' conflict, with the given content on each side, and returns the parsed file
    async Task<ConflictFile> ConflictedFileAsync(string baseText, string ourText, string theirText)
    {
        await repo.CommitFileAsync("file.txt", baseText, "Initial");
        Ok(await repo.Git.CreateBranchAsync("dev", true, repo.Path));
        await repo.CommitFileAsync("file.txt", theirText, "Dev work");
        Ok(await repo.Git.CheckoutAsync("main", repo.Path));
        await repo.CommitFileAsync("file.txt", ourText, "Main work");
        await repo.Git.MergeBranchAsync("dev", repo.Path);

        var result = await repo.Git.GetConflictFileAsync("file.txt", ConflictKind.BothModified, repo.Path);
        Assert.IsTrue(Try(out var file, out var e, result), $"{e}");
        return file;
    }

    [TestMethod]
    public async Task TestReadingAConflictedFileGivesItsTwoSides()
    {
        var file = await ConflictedFileAsync("a\nb\nc\n", "a\nOURS\nc\n", "a\nTHEIRS\nc\n");

        Assert.AreEqual(1, file.Hunks.Count);
        Assert.AreEqual("OURS\n", ConflictParser.ToText(file.Hunks[0].Ours));
        Assert.AreEqual("THEIRS\n", ConflictParser.ToText(file.Hunks[0].Theirs));
        Assert.AreEqual("HEAD", file.Hunks[0].OursLabel);
        Assert.AreEqual("dev", file.Hunks[0].TheirsLabel, "Git labels the other side with the branch merged in");
    }

    // Writing back an unchanged file must produce the same bytes, or every resolution would show up
    // as a whole file rewrite in the diff
    [TestMethod]
    public async Task TestWritingBackAnUnchangedFileChangesNothing()
    {
        var file = await ConflictedFileAsync("a\nb\nc\n", "a\nOURS\nc\n", "a\nTHEIRS\nc\n");
        var before = await File.ReadAllBytesAsync(Path.Join(repo.Path, "file.txt"));

        Ok(await repo.Git.WriteConflictFileAsync(file, repo.Path));

        var after = await File.ReadAllBytesAsync(Path.Join(repo.Path, "file.txt"));
        CollectionAssert.AreEqual(before, after, "Byte for byte the same file");
    }

    [TestMethod]
    public async Task TestResolvingAndWritingStagesThePathAsResolved()
    {
        var file = await ConflictedFileAsync("a\nb\nc\n", "a\nOURS\nc\n", "a\nTHEIRS\nc\n");

        Ok(await repo.Git.WriteConflictFileAsync(ConflictParser.SetChoice(file, 0, HunkChoice.Ours), repo.Path));

        Assert.AreEqual("a\nOURS\nc\n", await File.ReadAllTextAsync(Path.Join(repo.Path, "file.txt")));
        var status = Value(await repo.Git.GetStatusAsync(repo.Path));
        Assert.AreEqual(0, status.Conflicted, "Writing also marks it resolved");
        Assert.AreEqual(0, Value(await repo.Git.GetLeftoverMarkerPathsAsync(repo.Path)).Count);
    }

    // The one that says the per line terminators are doing their job: resolving one conflict in a
    // CRLF file must not convert the rest of it
    [TestMethod]
    public async Task TestResolvingACrLfFileKeepsCrLf()
    {
        var file = await ConflictedFileAsync("a\r\nb\r\nc\r\n", "a\r\nOURS\r\nc\r\n", "a\r\nTHEIRS\r\nc\r\n");

        Ok(await repo.Git.WriteConflictFileAsync(ConflictParser.SetChoice(file, 0, HunkChoice.Theirs), repo.Path));

        var text = await File.ReadAllTextAsync(Path.Join(repo.Path, "file.txt"));
        Assert.AreEqual("a\r\nTHEIRS\r\nc\r\n", text);
    }

    // Git, not gmd, is what adds a final newline to a file that had none: the conflict it writes
    // ends '>>>>>>> dev\n' and puts a newline after 'OURS' so the '=======' can start a line, so
    // the file's missing terminator is not represented in the conflict at all and cannot be put
    // back. What gmd must not do is add one of its own — hence writing the file back unchanged is
    // asserted above, and the parser's own no-trailing-newline round trip in ConflictParserTest.
    [TestMethod]
    public async Task TestGitItselfNormalizesAMissingFinalNewline()
    {
        var file = await ConflictedFileAsync("a\nb", "a\nOURS", "a\nTHEIRS");
        var conflicted = await File.ReadAllTextAsync(Path.Join(repo.Path, "file.txt"));
        Assert.IsTrue(conflicted.EndsWith(">>>>>>> dev\n"), $"Git ended the conflict with a newline:\n{conflicted}");

        Ok(await repo.Git.WriteConflictFileAsync(ConflictParser.SetChoice(file, 0, HunkChoice.Ours), repo.Path));

        Assert.AreEqual("a\nOURS\n", await File.ReadAllTextAsync(Path.Join(repo.Path, "file.txt")));
    }

    // 'git add' would happily stage a file whose BOM gmd had dropped, so the whole file would show
    // as changed
    [TestMethod]
    public async Task TestABomIsKeptAcrossAResolve()
    {
        var bom = "﻿";
        var file = await ConflictedFileAsync($"{bom}a\nb\nc\n", $"{bom}a\nOURS\nc\n", $"{bom}a\nTHEIRS\nc\n");
        Assert.IsTrue(file.HasBom, "The file was read as having a BOM");

        Ok(await repo.Git.WriteConflictFileAsync(ConflictParser.SetChoice(file, 0, HunkChoice.Ours), repo.Path));

        var bytes = await File.ReadAllBytesAsync(Path.Join(repo.Path, "file.txt"));
        CollectionAssert.AreEqual(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
    }

    // TempRepo pins 'merge.conflictStyle' to 'merge', but a user may well have set diff3 or zdiff3,
    // in which case git writes the common ancestor into the file itself and the parser has to read
    // it. Note the label is a bare sha rather than a phrase, which is why nothing keys off its text.
    [TestMethod]
    public async Task TestZdiff3ConflictStyleIsParsedWithItsBase()
    {
        await repo.GitAsync("config merge.conflictStyle zdiff3");
        var file = await ConflictedFileAsync("a\nb\nc\n", "a\nOURS\nc\n", "a\nTHEIRS\nc\n");

        var hunk = file.Hunks[0];

        Assert.IsTrue(hunk.HasBase, "zdiff3 records the common ancestor in the file");
        Assert.AreEqual("b\n", ConflictParser.ToText(hunk.Base));
        Assert.AreNotEqual("", hunk.BaseLabel, "Git labels it with the merge base sha");
    }

    // Choosing a side must drop the '|||||||' section along with the markers
    [TestMethod]
    public async Task TestResolvingAZdiff3ConflictLeavesNoBaseSection()
    {
        await repo.GitAsync("config merge.conflictStyle zdiff3");
        var file = await ConflictedFileAsync("a\nb\nc\n", "a\nOURS\nc\n", "a\nTHEIRS\nc\n");

        Ok(await repo.Git.WriteConflictFileAsync(ConflictParser.SetChoice(file, 0, HunkChoice.Ours), repo.Path));

        Assert.AreEqual("a\nOURS\nc\n", await File.ReadAllTextAsync(Path.Join(repo.Path, "file.txt")));
        Assert.AreEqual(0, Value(await repo.Git.GetLeftoverMarkerPathsAsync(repo.Path)).Count);
    }

    // ---- the common ancestor ----

    // The guard for the one thing in this feature that can be silently wrong. The base is recovered
    // by re-running the merge with '--diff3' over the index stages, and paired with the conflicts of
    // the working tree file *by position* — so if the two ever produced different regions, every
    // base would be shown against the wrong conflict and nothing would look amiss.
    [TestMethod]
    public async Task TestRecoveredBasesLineUpWithTheWorkingTreeConflicts()
    {
        var lines = Enumerable.Range(1, 30).Select(i => $"line {i}").ToList();
        await repo.CommitFileAsync("file.txt", string.Join('\n', lines) + "\n", "Initial");

        Ok(await repo.Git.CreateBranchAsync("dev", true, repo.Path));
        var theirs = lines.ToList();
        theirs[2] = "L3 THEIRS";
        theirs[11] = "L12 THEIRS";
        theirs[24] = "L25 THEIRS";
        await repo.CommitFileAsync("file.txt", string.Join('\n', theirs) + "\n", "Dev work");

        Ok(await repo.Git.CheckoutAsync("main", repo.Path));
        var ours = lines.ToList();
        ours[2] = "L3 OURS";
        ours[11] = "L12 OURS";
        ours[24] = "L25 OURS";
        await repo.CommitFileAsync("file.txt", string.Join('\n', ours) + "\n", "Main work");
        await repo.Git.MergeBranchAsync("dev", repo.Path);

        var file = Value(await repo.Git.GetConflictFileAsync("file.txt", ConflictKind.BothModified, repo.Path));
        Assert.AreEqual(3, file.Hunks.Count);
        Assert.IsFalse(file.Hunks[0].HasBase, "The default 'merge' style records no ancestor");

        var withBase = Value(await repo.Git.WithBaseAsync(file, repo.Path));

        Assert.AreEqual(3, withBase.Hunks.Count, "Recovering the base must not change the conflicts");
        Assert.AreEqual("line 3\n", ConflictParser.ToText(withBase.Hunks[0].Base));
        Assert.AreEqual("line 12\n", ConflictParser.ToText(withBase.Hunks[1].Base));
        Assert.AreEqual("line 25\n", ConflictParser.ToText(withBase.Hunks[2].Base));

        // Each side is still what it was, i.e. the bases were added and nothing else moved
        Assert.AreEqual("L3 OURS\n", ConflictParser.ToText(withBase.Hunks[0].Ours));
        Assert.AreEqual("L25 THEIRS\n", ConflictParser.ToText(withBase.Hunks[2].Theirs));
    }

    // The case that makes mapping by position wrong, against real git rather than by hand. Two
    // changes with one common line between them come back from 'git merge' as a single conflict and
    // from 'merge-file --diff3' as two — so taking its conflicts in order would leave this file's
    // one conflict holding only the first ancestor, silently missing the rest.
    [TestMethod]
    public async Task TestBaseOfAConflictThatDiff3WouldSplit()
    {
        var file = await ConflictedFileAsync(
            "head\nalpha\nmid\nbeta\ntail\n",
            "head\nOURS alpha\nmid\nOURS beta\ntail\n",
            "head\nTHEIRS alpha\nmid\nTHEIRS beta\ntail\n"
        );
        Assert.AreEqual(1, file.Hunks.Count, "Git wrote the two changes as one conflict");

        var withBase = Value(await repo.Git.WithBaseAsync(file, repo.Path));

        Assert.AreEqual(1, withBase.Hunks.Count);
        Assert.AreEqual(
            "alpha\nmid\nbeta\n",
            ConflictParser.ToText(withBase.Hunks[0].Base),
            "The whole ancestor of the joined region, including the common line between the two"
        );
    }

    // Nothing may appear in the working tree: 'git checkout-index --temp' would have been the
    // obvious way to get the stages onto disk, but it writes to the worktree root whatever its cwd,
    // and those files then show up as untracked in the very status gmd is displaying
    [TestMethod]
    public async Task TestRecoveringTheBaseLeavesNoTraceInTheWorkingTree()
    {
        var file = await ConflictedFileAsync("a\nb\nc\n", "a\nOURS\nc\n", "a\nTHEIRS\nc\n");
        var before = await repo.GitAsync("status --porcelain");

        Value(await repo.Git.WithBaseAsync(file, repo.Path));

        Assert.AreEqual(before, await repo.GitAsync("status --porcelain"), "No stray files");
        var strays = Directory.GetFiles(repo.Path, ".merge_file_*");
        Assert.AreEqual(0, strays.Length, $"Left behind: {string.Join(", ", strays)}");
        var scratch = Directory.GetDirectories(Path.Join(repo.Path, ".git"), "gmd-conflict-*");
        Assert.AreEqual(0, scratch.Length, "The scratch folder is removed");
    }

    // Already recorded in the file, so there is nothing to recover and no git command to run
    [TestMethod]
    public async Task TestZdiff3BaseIsKeptAsItIs()
    {
        await repo.GitAsync("config merge.conflictStyle zdiff3");
        var file = await ConflictedFileAsync("a\nb\nc\n", "a\nOURS\nc\n", "a\nTHEIRS\nc\n");

        var withBase = Value(await repo.Git.WithBaseAsync(file, repo.Path));

        Assert.AreEqual("b\n", ConflictParser.ToText(withBase.Hunks[0].Base));
    }

    // Both sides created the file, so there is no ancestor to show — and no error either
    [TestMethod]
    public async Task TestAddAddConflictHasNoBase()
    {
        await repo.CommitFileAsync("other.txt", "x\n", "Initial");
        Ok(await repo.Git.CreateBranchAsync("dev", true, repo.Path));
        await repo.CommitFileAsync("both.txt", "theirs\n", "Dev work");
        Ok(await repo.Git.CheckoutAsync("main", repo.Path));
        await repo.CommitFileAsync("both.txt", "ours\n", "Main work");
        await repo.Git.MergeBranchAsync("dev", repo.Path);

        var file = Value(await repo.Git.GetConflictFileAsync("both.txt", ConflictKind.BothAdded, repo.Path));
        var withBase = Value(await repo.Git.WithBaseAsync(file, repo.Path));

        Assert.AreEqual(1, withBase.Hunks.Count);
        Assert.IsFalse(withBase.Hunks[0].HasBase, "There is no common ancestor of two independent adds");
    }

    // A CRLF file goes through unpack-file and back as bytes, never through ICmd's stdout, which
    // strips carriage returns — so the recovered base is the real one rather than a mangled copy
    [TestMethod]
    public async Task TestBaseOfACrLfFileKeepsItsLineEndings()
    {
        var file = await ConflictedFileAsync("a\r\nb\r\nc\r\n", "a\r\nOURS\r\nc\r\n", "a\r\nTHEIRS\r\nc\r\n");

        var withBase = Value(await repo.Git.WithBaseAsync(file, repo.Path));

        Assert.AreEqual(1, withBase.Hunks.Count);
        Assert.AreEqual("b", withBase.Hunks[0].Base[0].Text, "The text is the line without its terminator");
        Assert.AreEqual("\r\n", withBase.Hunks[0].Base[0].Eol, "And the terminator survived");
    }

    [TestMethod]
    public async Task TestUseWholeFileTakesOneSideAndResolvesIt()
    {
        await ConflictedFileAsync("a\nb\nc\n", "a\nOURS\nc\n", "a\nTHEIRS\nc\n");

        Ok(await repo.Git.UseWholeFileAsync("file.txt", isOurs: false, repo.Path));

        Assert.AreEqual("a\nTHEIRS\nc\n", await File.ReadAllTextAsync(Path.Join(repo.Path, "file.txt")));
        Assert.AreEqual(0, Value(await repo.Git.GetStatusAsync(repo.Path)).Conflicted);
    }

    // Un-resolving puts the markers back, which git can do from the resolve-undo data in the index
    // even after the path was staged
    [TestMethod]
    public async Task TestUnresolvePutsTheConflictBack()
    {
        var file = await ConflictedFileAsync("a\nb\nc\n", "a\nOURS\nc\n", "a\nTHEIRS\nc\n");
        Ok(await repo.Git.WriteConflictFileAsync(ConflictParser.SetChoice(file, 0, HunkChoice.Ours), repo.Path));

        Ok(await repo.Git.UnresolveAsync("file.txt", repo.Path));

        var status = Value(await repo.Git.GetStatusAsync(repo.Path));
        Assert.AreEqual(1, status.Conflicted, "It is an unmerged path again");
        StringAssert.Contains(await File.ReadAllTextAsync(Path.Join(repo.Path, "file.txt")), "<<<<<<<");
    }

    // A path whose name contains glob characters, which '--' alone does not protect
    [TestMethod]
    public async Task TestPathWithGlobCharactersIsMatchedLiterally()
    {
        await repo.CommitFileAsync("a1.txt", "decoy\n", "Decoy");
        await repo.CommitFileAsync("a[1].txt", "one\n", "Initial");
        Ok(await repo.Git.CreateBranchAsync("dev", true, repo.Path));
        await repo.CommitFileAsync("a[1].txt", "dev\n", "Dev work");
        Ok(await repo.Git.CheckoutAsync("main", repo.Path));
        await repo.CommitFileAsync("a[1].txt", "main\n", "Main work");
        await repo.Git.MergeBranchAsync("dev", repo.Path);

        Ok(await repo.Git.UseWholeFileAsync("a[1].txt", isOurs: true, repo.Path));

        Assert.AreEqual(0, Value(await repo.Git.GetStatusAsync(repo.Path)).Conflicted);
        Assert.AreEqual(
            "decoy\n",
            await File.ReadAllTextAsync(Path.Join(repo.Path, "a1.txt")),
            "The decoy is untouched"
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
