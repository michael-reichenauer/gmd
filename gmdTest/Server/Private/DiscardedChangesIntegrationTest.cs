using gmd.Server;
using gmd.Server.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server.Private;

// What a discard throws away, and a stash dropped, kept and put back, over a real repository: the
// working tree before and after a discard as commits on gmd's own ref, and their difference applied
// again, which brings the working tree back as it was, the new files untracked again and nothing
// staged.
//
// The tests are in their own category, so they can be excluded when only the fast tests are
// wanted:  scripts/test --filter "TestCategory!=Integration"
[TestClass]
[TestCategory("Integration")]
public class DiscardedChangesIntegrationTest
{
    TempRepo repo = null!;
    DiscardedChanges discarded = null!;

    [TestInitialize]
    public async Task Init()
    {
        repo = await TempRepo.CreateAsync();
        discarded = new DiscardedChanges(repo.Git);
        await repo.CommitFileAsync("a.txt", "one\ntwo\n", "A");
        await repo.CommitFileAsync(".gitignore", "*.log\n", "Ignore logs");
    }

    [TestCleanup]
    public void Cleanup() => repo.Dispose();

    string PathOf(string name) => Path.Join(repo.Path, name);

    async Task<string> StatusAsync() => (await repo.GitAsync("status --porcelain --ignored")).TrimEnd();

    // Everything discarded, a changed file, a new one and a binary one, is put back as it was; a file
    // git ignores is neither kept nor touched
    [TestMethod]
    public async Task TestAllChangesDiscardedArePutBack()
    {
        repo.WriteFile("a.txt", "one\nTWO\n");
        repo.WriteFile("new.txt", "new\n");
        File.WriteAllBytes(PathOf("image.bin"), [0, 1, 2, 255, 0]);
        repo.WriteFile("debug.log", "log\n");
        var before = await StatusAsync();

        AssertOk(
            await discarded.DiscardAsync(
                "All changes",
                () => repo.Git.UndoAllUncommittedChangesAsync(repo.Path),
                repo.Path
            )
        );
        Assert.AreEqual("!! debug.log", await StatusAsync(), "Discarded, the ignored file left");

        var change = AssertOk(await discarded.GetAsync(repo.Path)).Single();
        Assert.AreEqual("All changes", change.What);
        Assert.IsFalse(change.IsStash);
        AssertOk(await discarded.RestoreAsync(change, repo.Path));

        Assert.AreEqual(before, await StatusAsync());
        Assert.AreEqual("one\nTWO\n", File.ReadAllText(PathOf("a.txt")));
        CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 255, 0 }, File.ReadAllBytes(PathOf("image.bin")));
    }

    // A discard of part of the changes is put back alone, the rest of them as they are, and the
    // records are newest first
    [TestMethod]
    public async Task TestEachDiscardIsPutBackAlone()
    {
        repo.WriteFile("a.txt", "ONE\ntwo\nthree\n");
        repo.WriteFile("b.txt", "bee\n");
        AssertOk(
            await discarded.DiscardAsync(
                "b.txt",
                () => repo.Git.UndoUncommittedFileAsync("b.txt", repo.Path),
                repo.Path
            )
        );
        // The first line discarded, as Discard Lines does
        AssertOk(
            await discarded.DiscardAsync(
                "1 line of a.txt",
                () =>
                {
                    repo.WriteFile("a.txt", "one\ntwo\nthree\n");
                    return Task.FromResult(Result.Ok);
                },
                repo.Path
            )
        );

        var changes = AssertOk(await discarded.GetAsync(repo.Path));
        Assert.AreEqual("1 line of a.txt, b.txt", string.Join(", ", changes.Select(c => c.What)));
        AssertOk(await discarded.RestoreAsync(changes[1], repo.Path));

        Assert.AreEqual("bee\n", File.ReadAllText(PathOf("b.txt")));
        Assert.AreEqual("one\ntwo\nthree\n", File.ReadAllText(PathOf("a.txt")), "The other discard is left as it is");
    }

    // A discard that throws nothing away, the files being as they were, is no record
    [TestMethod]
    public async Task TestNothingDiscardedIsNoRecord()
    {
        repo.WriteFile("a.txt", "one\nTWO\n");

        AssertOk(await discarded.DiscardAsync("Nothing", () => Task.FromResult(Result.Ok), repo.Path));

        Assert.AreEqual(0, AssertOk(await discarded.GetAsync(repo.Path)).Count);
    }

    // A file changed since it was discarded is not put back over, nor any part of it: git applies all
    // of a difference or none of it
    [TestMethod]
    public async Task TestAFileChangedSinceIsNotRestoredOver()
    {
        repo.WriteFile("a.txt", "one\nTWO\n");
        AssertOk(
            await discarded.DiscardAsync(
                "a.txt",
                () => repo.Git.UndoUncommittedFileAsync("a.txt", repo.Path),
                repo.Path
            )
        );
        repo.WriteFile("a.txt", "one\nchanged since\n");

        AssertError(await discarded.RestoreAsync(AssertOk(await discarded.GetAsync(repo.Path)).Single(), repo.Path));

        Assert.AreEqual("one\nchanged since\n", File.ReadAllText(PathOf("a.txt")));
    }

    // A dropped stash is kept, and stored back as a stash
    [TestMethod]
    public async Task TestADroppedStashIsStoredBack()
    {
        repo.WriteFile("a.txt", "one\nstashed\n");
        AssertOk(await repo.Git.StashAsync("Experiment", repo.Path));
        var stash = AssertOk(await repo.Git.GetStashesAsync(repo.Path)).Single();

        AssertOk(await discarded.DropStashAsync(stash, repo.Path));
        Assert.AreEqual(0, AssertOk(await repo.Git.GetStashesAsync(repo.Path)).Count);

        var change = AssertOk(await discarded.GetAsync(repo.Path)).Single();
        Assert.IsTrue(change.IsStash);
        AssertOk(await discarded.RestoreAsync(change, repo.Path));

        var restored = AssertOk(await repo.Git.GetStashesAsync(repo.Path)).Single();
        Assert.AreEqual(stash.Id, restored.Id);
        AssertOk(await repo.Git.StashPopAsync(restored.Name, repo.Path));
        Assert.AreEqual("one\nstashed\n", File.ReadAllText(PathOf("a.txt")));
    }
}
