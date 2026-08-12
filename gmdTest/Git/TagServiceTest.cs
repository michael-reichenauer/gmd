using gmd.Git.Private;
using gmdTest.Utils;

namespace gmdTest.Git;

[TestClass]
public class TagServiceTest
{
    // Output of: git show-ref --dereference --tags
    // 'v2.0' is an annotated tag, so --dereference gives it twice: once as the tag object and once
    // as the commit it points at, the latter suffixed with '^{}'.
    const string TagsOutput = """
        134e1960d41fc44fb5ffffde38c2273f5e9910fc refs/tags/release/3.0
        134e1960d41fc44fb5ffffde38c2273f5e9910fc refs/tags/v1.0
        d61063ef7b773d3efe325b1067787b17fb89bfc7 refs/tags/v2.0
        134e1960d41fc44fb5ffffde38c2273f5e9910fc refs/tags/v2.0^{}
        """;

    static TagService NewService(ICmd cmd) => new TagService(cmd);

    static async Task<IReadOnlyList<gmd.Git.Tag>> GetTagsAsync(ICmd cmd)
    {
        var result = await NewService(cmd).GetTagsAsync("/wd");
        Assert.IsTrue(Try(out var tags, out var e, result), $"GetTagsAsync failed: {e}");
        return tags;
    }

    // The 'refs/tags/' prefix is dropped, and a tag name may contain '/'
    [TestMethod]
    public async Task TestParseTagNamesAndCommitIds()
    {
        var tags = await GetTagsAsync(new FakeCmd(TagsOutput));

        CollectionAssert.AreEqual(new[] { "release/3.0", "v1.0", "v2.0", "v2.0" }, tags.Select(t => t.Name).ToArray());
        Assert.AreEqual("134e1960d41fc44fb5ffffde38c2273f5e9910fc", tags[0].CommitId);
    }

    // An annotated tag appears twice, once pointing at the tag object and once, with the '^{}'
    // suffix trimmed, at the commit. Augmenter.AddAugTags drops the first one, since no commit in
    // the log has the tag object's id.
    [TestMethod]
    public async Task TestParseAnnotatedTagIsListedTwice()
    {
        var tags = await GetTagsAsync(new FakeCmd(TagsOutput));

        var annotated = tags.Where(t => t.Name == "v2.0").ToList();
        Assert.AreEqual(2, annotated.Count);
        Assert.AreEqual("d61063ef7b773d3efe325b1067787b17fb89bfc7", annotated[0].CommitId, "The tag object");
        Assert.AreEqual("134e1960d41fc44fb5ffffde38c2273f5e9910fc", annotated[1].CommitId, "The commit");
    }

    // In a repo with no tags git writes nothing and exits with an error, which is not a failure
    [TestMethod]
    public async Task TestNoTagsIsAnEmptyListNotAnError()
    {
        var tags = await GetTagsAsync(new FakeCmd((_, _, _) => FakeCmd.Fail("")));

        Assert.AreEqual(0, tags.Count);
    }

    [TestMethod]
    public async Task TestGitCommandFailureIsPropagated()
    {
        var service = NewService(new FakeCmd((_, _, _) => FakeCmd.Fail("fatal: not a git repository")));

        var result = await service.GetTagsAsync("/wd");

        Assert.IsFalse(Try(out var _, out var _, result), "Expected the git failure to propagate");
    }

    [TestMethod]
    public async Task TestGetTagsPassesArgsAndWorkingDirectoryToGit()
    {
        var cmd = new FakeCmd(TagsOutput);

        await NewService(cmd).GetTagsAsync("/some/wd");

        Assert.AreEqual("git", cmd.Calls[0].Path);
        Assert.AreEqual("/some/wd", cmd.Calls[0].WorkingDirectory);
        Assert.AreEqual("show-ref --dereference --tags", cmd.Calls[0].Args);
    }

    [TestMethod]
    public async Task TestAddTag()
    {
        var cmd = new FakeCmd("");

        await NewService(cmd).AddTagAsync("v1.0", "abc123", "/wd");

        Assert.AreEqual("tag v1.0 abc123", cmd.Calls[0].Args);
    }

    [TestMethod]
    public async Task TestAddAnnotatedTag()
    {
        var cmd = new FakeCmd("");

        await NewService(cmd).AddAnnotatedTagAsync("v1.0", "The message", "abc123", "/wd");

        Assert.AreEqual("tag -a v1.0 abc123 -m \"The message\"", cmd.Calls[0].Args);
    }

    [TestMethod]
    public async Task TestRemoveTag()
    {
        var cmd = new FakeCmd("");

        await NewService(cmd).RemoveTagAsync("v1.0", "/wd");

        Assert.AreEqual("tag -d v1.0", cmd.Calls[0].Args);
    }

    // ---------------------------------------------------------------------------------------
    // Pruning the local tags the remote deleted. The tracked remote tags are gmd's own
    // remote-tracking namespace for tags, which is the record that tells a tag the remote deleted
    // from one that was never pushed — see the comment in TagService.

    const string Id1 = "134e1960d41fc44fb5ffffde38c2273f5e9910fc";
    const string Id2 = "d61063ef7b773d3efe325b1067787b17fb89bfc7";

    // Output of: git for-each-ref --format="%(objectname) %(refname:strip=N)" <ref>
    static FakeCmd RefsCmd(string trackedRefs, string localRefs) =>
        new FakeCmd(
            (_, args, _) =>
                args.StartsWith("for-each-ref")
                    ? FakeCmd.Ok(args.Contains("refs/gmdtags/") ? trackedRefs : localRefs)
                    : FakeCmd.Ok("")
        );

    static IReadOnlyDictionary<string, string> Tracked(params (string Name, string Id)[] tags) =>
        tags.ToDictionary(t => t.Name, t => t.Id);

    static string[] DeletedTags(FakeCmd cmd) =>
        cmd.Calls.Where(c => c.Args.StartsWith("tag -d ")).Select(c => c.Args.TrimPrefix("tag -d ")).ToArray();

    [TestMethod]
    public async Task TestTrackedRemoteTagsAreReadFromTheirOwnNamespace()
    {
        var cmd = new FakeCmd($"{Id1} v1.0\n{Id2} release/3.0");

        var result = await NewService(cmd).GetTrackedRemoteTagsAsync("/some/wd");

        Assert.IsTrue(Try(out var tracked, out var e, result), $"Failed: {e}");
        Assert.AreEqual(
            "for-each-ref --format=\"%(objectname) %(refname:strip=3)\" refs/gmdtags/origin/",
            cmd.Calls[0].Args
        );
        Assert.AreEqual("/some/wd", cmd.Calls[0].WorkingDirectory);
        Assert.AreEqual(Id1, tracked["v1.0"]);
        Assert.AreEqual(Id2, tracked["release/3.0"], "A tag name may contain '/', so strip= is by namespace depth");
    }

    // The whole point: the tag is gone from the remote, so the local one goes with it
    [TestMethod]
    public async Task TestTagDeletedOnTheRemoteIsDeletedLocally()
    {
        var cmd = RefsCmd(trackedRefs: $"{Id2} v2.0", localRefs: $"{Id1} v1.0\n{Id2} v2.0");

        await NewService(cmd).PruneDeletedRemoteTagsAsync(Tracked(("v1.0", Id1), ("v2.0", Id2)), "/wd");

        CollectionAssert.AreEqual(new[] { "v1.0" }, DeletedTags(cmd));
    }

    // The case --prune-tags got wrong, and the reason for all of this: a tag that was never pushed
    // is not in the tracked remote tags at all, so it is not something the remote deleted
    [TestMethod]
    public async Task TestNeverPushedTagIsKept()
    {
        var cmd = RefsCmd(trackedRefs: $"{Id2} v2.0", localRefs: $"{Id1} local-only\n{Id2} v2.0");

        await NewService(cmd).PruneDeletedRemoteTagsAsync(Tracked(("v2.0", Id2)), "/wd");

        CollectionAssert.AreEqual(Array.Empty<string>(), DeletedTags(cmd));
    }

    // Moved locally since it was seen on the remote, so the local ref is no longer the remote's
    [TestMethod]
    public async Task TestLocallyRetaggedTagIsKept()
    {
        var cmd = RefsCmd(trackedRefs: "", localRefs: $"{Id2} v1.0");

        await NewService(cmd).PruneDeletedRemoteTagsAsync(Tracked(("v1.0", Id1)), "/wd");

        CollectionAssert.AreEqual(Array.Empty<string>(), DeletedTags(cmd));
    }

    // The first fetch after upgrading, where the namespace is only being filled in. Nothing has
    // been observed on the remote yet, so nothing can be known to have been deleted — which is what
    // makes turning this on safe rather than a flag day.
    [TestMethod]
    public async Task TestNothingIsPrunedWhenNothingHasBeenObservedYet()
    {
        var cmd = RefsCmd(trackedRefs: "", localRefs: $"{Id1} v1.0");

        await NewService(cmd).PruneDeletedRemoteTagsAsync(Tracked(), "/wd");

        CollectionAssert.AreEqual(Array.Empty<string>(), DeletedTags(cmd));
        Assert.AreEqual(0, cmd.Calls.Count, "Expected no git command at all");
    }

    // Deleted by hand between the fetch and this, so there is nothing to delete
    [TestMethod]
    public async Task TestTagAlreadyGoneLocallyIsNotDeletedAgain()
    {
        var cmd = RefsCmd(trackedRefs: "", localRefs: "");

        await NewService(cmd).PruneDeletedRemoteTagsAsync(Tracked(("v1.0", Id1)), "/wd");

        CollectionAssert.AreEqual(Array.Empty<string>(), DeletedTags(cmd));
    }
}
