using gmd.Cui;
using gmd.Server;
using gmdTest.Fixtures;

namespace gmdTest.Cui;

// The rows of the tags dialog: newest commit first, and whether origin has each tag
[TestClass]
public class TagRowsTest
{
    static async Task<Repo> RepoAsync() =>
        await new RepoBuilder()
            .Commit("c2", "Second", "c1")
            .Commit("c1", "Initial")
            .BranchWithRemote("main", "c2", isCurrent: true)
            .ViewRepoAsync();

    [TestMethod]
    public async Task TestTheTagsAreInTheOrderOfTheirCommits()
    {
        var repo = await RepoAsync();
        RepoTag[] tags =
        [
            new("v0.1", RepoBuilder.Sha("c1"), true),
            new("gone", RepoBuilder.Sha("f9"), false),
            new("v0.2", RepoBuilder.Sha("c2"), false),
        ];

        var items = TagRows.Items(tags, repo, hasOrigin: true);

        CollectionAssert.AreEqual(new[] { "v0.2", "v0.1", "gone" }, items.Select(i => i.Tag.Name).ToArray());
        Assert.IsNull(items[2].Commit, "Not in the log, so last");
    }

    [TestMethod]
    public async Task TestARowSaysWhetherOriginHasTheTag()
    {
        var repo = await RepoAsync();
        var items = TagRows.Items(
            [new("v0.2", RepoBuilder.Sha("c2"), false), new("v0.1", RepoBuilder.Sha("c1"), true)],
            repo,
            hasOrigin: true
        );
        var c2 = RepoBuilder.Sid("c2");
        var c1 = RepoBuilder.Sid("c1");

        Assert.AreEqual(
            "Tag                 Commit  Date        Origin      Subject             ",
            TagRows.Header(TagRows.MinWidth).ToString()
        );
        Assert.AreEqual(
            $"v0.2                {c2}  {repo.CommitById[RepoBuilder.Sha("c2")].AuthorTime.IsoDate()}  not pushed  Second              ",
            TagRows.Row(items[0], TagRows.MinWidth).ToString()
        );
        Assert.AreEqual($"v0.2 on {c2} of 'main', not on origin: Push pushes it", TagRows.About(items[0]));
        Assert.AreEqual(
            $"v0.1 on {c1} of 'main', on origin too: Remove removes it there as well",
            TagRows.About(items[1])
        );
        Assert.IsTrue(items[0].CanPush);
        Assert.IsFalse(items[1].CanPush);
    }

    // With no origin there is nowhere to push to, and nothing to say about it
    [TestMethod]
    public async Task TestWithNoOriginATagCannotBePushed()
    {
        var repo = await RepoAsync();
        var item = TagRows.Items([new("v0.2", RepoBuilder.Sha("c2"), false)], repo, hasOrigin: false).Single();

        Assert.IsFalse(item.CanPush);
        Assert.AreEqual($"v0.2 on {RepoBuilder.Sid("c2")} of 'main'", TagRows.About(item));
    }
}
