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

    static TagService NewService(ICmd cmd) => new TagService(cmd, new RemoteService(cmd));

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
}
