using gmd.Server.Private.Augmented.Private;

namespace gmdTest.Augmented;

// Characterization tests for the merge subject parser, which is how gmd recovers the name of a
// branch that git has forgotten (i.e. a branch that was deleted after being merged). The regex is
// a single expression covering many git clients, so these pin down what each real world subject
// form currently yields, including the forms it gets wrong.
[TestClass]
public class BranchNameServiceTest
{
    static FromInto Parse(string subject) => new BranchNameService().ParseSubject(subject);

    // The plain 'git merge' subject, and the case variants git and other clients produce
    [TestMethod]
    public void TestMergeBranchIntoBranch()
    {
        Assert.AreEqual(new FromInto("develop", "main", false, false), Parse("Merge branch 'develop' into main"));
        Assert.AreEqual(new FromInto("dev", "main", false, false), Parse("merged branch 'dev' into main"));
        Assert.AreEqual(new FromInto("dev", "main", false, false), Parse("Merge branch 'dev' to main"));
        Assert.AreEqual(new FromInto("dev", "", false, false), Parse("Merge branch 'dev'"));
    }

    // 'git merge origin/dev' and friends. The origin prefix is trimmed so the name matches the
    // local branch name.
    [TestMethod]
    public void TestRemoteTrackingBranchPrefixesAreTrimmed()
    {
        Assert.AreEqual(
            new FromInto("dev", "main", false, false),
            Parse("Merge remote-tracking branch 'origin/dev' into main")
        );
        Assert.AreEqual(
            new FromInto("dev", "", false, false),
            Parse("Merge remote-tracking branch 'refs/remotes/origin/dev'")
        );
        Assert.AreEqual(new FromInto("dev", "", false, false), Parse("Merge remotes/origin/dev"));
    }

    // A pull merge is the same branch merged into itself, from the remote repository. It is
    // detected either by the 'of <url>' part or by from and into being the same name. gmd swaps
    // the parents of such a commit, so getting this right matters for the branch structure.
    [TestMethod]
    public void TestPullMergeIsDetected()
    {
        Assert.AreEqual(
            new FromInto("dev", "dev", true, false),
            Parse("Merge branch 'dev' of https://github.com/michael-reichenauer/gmd into dev")
        );
        Assert.AreEqual(new FromInto("main", "main", true, false), Parse("Merge branch 'main' into main"));
        Assert.AreEqual(new FromInto("dev", "dev", true, false), Parse("Merge branch 'dev' of git@github.com:x/y"));
    }

    // Merging another branch from a remote repository is not a pull merge, even though the
    // subject has the same 'of <url>' shape, since the branch is merged into a different branch
    [TestMethod]
    public void TestMergeOfOtherBranchFromRemoteIsNotAPullMerge()
    {
        Assert.AreEqual(
            new FromInto("dev", "other", false, false),
            Parse("Merge branch 'dev' of github.com:x/y into other")
        );
    }

    // GitHub and Azure DevOps pull request subjects. Both are flagged as pull requests and
    // neither says which branch was merged into.
    [TestMethod]
    public void TestPullRequestSubjects()
    {
        Assert.AreEqual(new FromInto("mich/dev", "", false, true), Parse("Merge pull request #1 from mich/dev"));
        Assert.AreEqual(
            new FromInto("octo-org/feature/x", "", false, true),
            Parse("Merge pull request #123 from octo-org/feature/x")
        );

        // Azure DevOps: the PR number becomes part of the name, since the branch name is not in
        // the subject at all
        Assert.AreEqual(new FromInto("PR456", "", false, true), Parse("Merged PR 456: Add a thing"));

        // Only the two wordings above are recognized as pull requests, 'Merge PR' is parsed as an
        // ordinary merge
        Assert.AreEqual(new FromInto("feature/x", "main", false, false), Parse("Merge PR feature/x into main"));
    }

    [TestMethod]
    public void TestSubjectWithoutABranchName()
    {
        Assert.AreEqual(new FromInto("", "", false, false), Parse("Just a normal commit subject"));
        Assert.AreEqual(new FromInto("", "", false, false), Parse(""));
    }

    // 'git merge <sha>' writes "Merge commit '<sha>'". A commit id is not a branch name, so it
    // says nothing about which branch the merged commit was on, only which branch it was merged
    // into.
    [TestMethod]
    public void TestMergeCommitNamesOnlyTheTargetBranch()
    {
        Assert.AreEqual(
            new FromInto("", "dev", false, false),
            Parse("Merge commit '60b16d764ac1e5d3f579693248f6ffe35da20beb' into dev")
        );
        Assert.AreEqual(new FromInto("", "", false, false), Parse("Merge commit '60b16d7'"));
    }

    // Dots are common in branch names, e.g. release branches. A git ref can neither start nor end
    // with a dot, so a name is never read past a sentence ending.
    [TestMethod]
    public void TestBranchNamesWithDots()
    {
        Assert.AreEqual(
            new FromInto("release/1.0", "develop", false, false),
            Parse("Merge branch 'release/1.0' into develop")
        );
        Assert.AreEqual(new FromInto("v1.2.x", "main", false, false), Parse("Merge branch 'v1.2.x' into main"));
        Assert.AreEqual(new FromInto("dev", "", false, false), Parse("Merged from dev."));
    }

    // GitLab quotes the target branch as well as the source branch
    [TestMethod]
    public void TestGitLabQuotesTheTargetBranch()
    {
        Assert.AreEqual(new FromInto("feature/x", "main", false, false), Parse("Merge branch 'feature/x' into 'main'"));
    }

    // Merging a tag is not merging a branch, but the merged commits still need a name to be shown
    // under, and the subject still says which branch they were merged into
    [TestMethod]
    public void TestMergeOfATag()
    {
        Assert.AreEqual(new FromInto("v1.2.3", "main", false, false), Parse("Merge tag 'v1.2.3' into main"));
    }

    // An octopus merge has more than two parents. Only the first merged branch gets a name, since
    // the parser answers "which branch is this one commit on".
    [TestMethod]
    public void TestOctopusMergeSubject()
    {
        Assert.AreEqual(new FromInto("a", "main", false, false), Parse("Merge branches 'a' and 'b' into main"));
        Assert.AreEqual(new FromInto("a", "main", false, false), Parse("Merge branches 'a', 'b' and 'c' into main"));
    }

    // Known limitation, pinned so that changing it is a deliberate choice
    [TestMethod]
    public void TestRevertOfAMergeIsParsedAsTheMerge()
    {
        // Harmless in practice, since only commits with two parents are parsed
        Assert.AreEqual(new FromInto("dev", "main", false, false), Parse("Revert \"Merge branch 'dev' into main\""));
    }
}
