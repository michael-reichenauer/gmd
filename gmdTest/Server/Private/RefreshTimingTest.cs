using System.Diagnostics;
using System.Text;
using gmd.Common;
using gmd.Cui;
using gmd.Cui.Common;
using gmd.Git;
using gmd.Server;
using gmd.Server.Private;
using gmd.Server.Private.Augmented.Private;
using gmdTest.Fixtures;

namespace gmdTest.Server.Private;

// Times each stage of reading and showing a real repository, so that the cost of a refresh on a
// large repository is measured rather than guessed. Only run when asked, by naming the repository
// to read and the file to write:
//
//   GMD_PERF_REPO=<repo> GMD_PERF_OUT=<file> dotnet test gmdTest/gmdTest.csproj --filter RefreshTiming
//
// Each stage runs once to warm up and then five times, and the median time is reported with what
// the stage allocated. The stages are those of a refresh: the git reads (AugmentedService), the
// augmentation, one stage at a time (BranchStructureService), the conversion to the repo the UI
// works with, the view repo of what is shown (ViewRepoCreater), for the default view, all live
// branches and all branches, and the graph drawn of each.
//
// Beside the timings, '<file>.view' gets what the view repos hold, which branches in which order and
// which commits are ahead or behind, so that a change made for speed can be shown to change nothing:
// two of those files, before and after, are compared with diff, as InferenceDumpTest's dumps are.
// The repository is only read, as InferenceDumpTest reads it, the status excepted.
[TestClass]
[TestCategory("Integration")]
public class RefreshTimingTest
{
    const int MaxCommitCount = 30000; // As many as AugmentedService reads
    const int Runs = 5;

    static string RepoPath => Environment.GetEnvironmentVariable("GMD_PERF_REPO") ?? "";
    static string OutPath => Environment.GetEnvironmentVariable("GMD_PERF_OUT") ?? "";

    readonly List<string> rows = [];

    [TestMethod]
    public async Task TimeRefresh()
    {
        if (RepoPath == "" || OutPath == "")
            Assert.Inconclusive("Only run when GMD_PERF_REPO and GMD_PERF_OUT name a repository and a file");

        var cmd = new Cmd();
        var git = TempRepo.NewGit(cmd);
        var metaDataService = new MetaDataService(git, new FakeRepoConfig());

        // The git reads, the log on its own and with the parse, and the rest the way the service reads them
        var logArgs = $"log --all --date-order -z --pretty=\"%H|%ai|%ci|%an|%P|%B\" --max-count={MaxCommitCount}";
        await TimeAsync("git log, read", async () => AssertOk(await cmd.RunAsync("git", logArgs, RepoPath)));
        var log = await TimeAsync(
            "git log, read + parse",
            async () => AssertOk(await git.GetLogAsync(MaxCommitCount, RepoPath))
        );
        var reads = await TimeAsync("git other reads, in parallel", () => OtherReadsAsync(git, metaDataService));

        var gitRepo = new GitRepo(
            DateTime.UtcNow,
            RepoPath,
            log,
            reads.Branches,
            reads.Tags,
            RepoBuilder.NoChanges,
            reads.MetaData,
            reads.Stashes,
            log.Count == MaxCommitCount,
            reads.Worktrees,
            reflog: reads.Reflog
        );

        // The augmentation, with the time of each stage of the branch structure pipeline
        var stages = new StageTimes();
        var augmenter = NewTimedAugmenter(stages);
        await TimeAsync("augment", () => augmenter.GetAugRepoAsync(gitRepo), stages);

        var workRepo = await augmenter.GetAugRepoAsync(gitRepo);
        var repo = Time("to repo", () => Uncommitted.Adjust(new WorkRepoConverter().ToRepo(workRepo)));

        var viewRepoCreater = new ViewRepoCreater(new ViewRepoConverter(), new FakeRepoConfig());
        var views = new StringBuilder();
        Repo viewRepo = repo;
        foreach (var show in new[] { ShowBranches.Specified, ShowBranches.AllActive, ShowBranches.AllActiveAndDeleted })
        {
            viewRepo = Time($"view repo, {show}", () => viewRepoCreater.GetViewRepoAsync(repo, [], show));
            Time(
                $"graph, {show}",
                () => new GraphCreater(new BranchColorService(new FakeRepoConfig())).Create(viewRepo)
            );
            AppendView(views, $"{show}", viewRepo);
        }

        // Once all branches are shown, every refresh shows them again by name (RepoView)
        var shownNames = viewRepo.ViewBranches.Select(b => b.Name).ToList();
        var refreshed = Time("view repo, refresh of all", () => viewRepoCreater.GetViewRepoAsync(repo, shownNames));
        AppendView(views, "Refresh of all", refreshed);

        var header =
            $"# Refresh timing of {Path.GetFileName(Path.GetFullPath(RepoPath).TrimEnd('/'))}: "
            + $"{workRepo.Commits.Count} commits, {workRepo.Branches.Count} branches "
            + $"({workRepo.Branches.Values.Count(b => b.IsGitBranch)} git branches), median of {Runs} runs";
        File.WriteAllText(OutPath, header + "\n" + string.Join("\n", rows) + "\n");
        File.WriteAllText(OutPath + ".view", views.ToString());
    }

    record OtherReads(
        IReadOnlyList<gmd.Git.Branch> Branches,
        IReadOnlyList<gmd.Git.Tag> Tags,
        IReadOnlyList<gmd.Git.Stash> Stashes,
        IReadOnlyList<gmd.Git.Worktree> Worktrees,
        IReadOnlyList<ReflogEntry> Reflog,
        MetaData MetaData
    );

    static async Task<OtherReads> OtherReadsAsync(IGit git, MetaDataService metaDataService)
    {
        var branchesTask = git.GetBranchesAsync(RepoPath);
        var tagsTask = git.GetTagsAsync(RepoPath);
        var stashesTask = git.GetStashesAsync(RepoPath);
        var worktreesTask = git.GetWorktreesAsync(RepoPath);
        var reflogTask = git.GetReflogAsync(RepoPath);
        var metaDataTask = metaDataService.GetMetaDataAsync(RepoPath);
        await Task.WhenAll(branchesTask, tagsTask, stashesTask, worktreesTask, reflogTask, metaDataTask);

        return new OtherReads(
            AssertOk(branchesTask.Result),
            AssertOk(tagsTask.Result),
            AssertOk(stashesTask.Result),
            AssertOk(worktreesTask.Result),
            AssertOk(reflogTask.Result),
            AssertOk(metaDataTask.Result)
        );
    }

    // The view repo as a text that changes only if what is shown changes
    static void AppendView(StringBuilder text, string show, Repo viewRepo)
    {
        text.AppendLine($"# {show}: {viewRepo.ViewBranches.Count} branches, {viewRepo.ViewCommits.Count} commits");
        foreach (var b in viewRepo.ViewBranches)
        {
            text.AppendLine(
                $"B {b.Name} {(b.HasLocalOnly ? "L" : "-")}{(b.HasRemoteOnly ? "R" : "-")} {b.ParentBranchName}"
            );
        }
        foreach (var c in viewRepo.ViewCommits)
        {
            text.AppendLine($"C {c.Sid} {(c.IsAhead ? "A" : "-")}{(c.IsBehind ? "B" : "-")} {c.BranchName}");
        }
    }

    T Time<T>(string stage, Func<T> run) => TimeAsync(stage, () => Task.FromResult(run())).Result;

    async Task<T> TimeAsync<T>(string stage, Func<Task<T>> run, StageTimes? stages = null)
    {
        var result = await run(); // Warm up, i.e. JIT and caches, as a refresh after the first one is
        stages?.Clear();

        List<(double ms, long bytes)> runs = [];
        for (var i = 0; i < Runs; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var bytes = GC.GetTotalAllocatedBytes(precise: true);
            var stopwatch = Stopwatch.StartNew();
            result = await run();
            runs.Add((stopwatch.Elapsed.TotalMilliseconds, GC.GetTotalAllocatedBytes(precise: true) - bytes));
        }

        var median = runs.OrderBy(r => r.ms).ElementAt(Runs / 2);
        rows.Add($"{stage, -40} {median.ms, 9:F1} ms {median.bytes / 1024.0 / 1024.0, 9:F1} MB");
        if (stages != null)
        {
            foreach (var (name, ms) in stages.Medians(Runs))
            {
                rows.Add($"  {name, -38} {ms, 9:F1} ms");
            }
        }
        return result;
    }

    // The augmenter as RepoBuilder.NewAugmenter() wires it, with each stage of the pipeline timed
    static IAugmenter NewTimedAugmenter(StageTimes stages)
    {
        var branchNameService = new BranchNameService();
        return new Augmenter(
            new BranchStructureService(
                branchNameService,
                new TimedCommitGraph(new CommitGraphService(branchNameService), stages),
                new TimedCommitBranch(
                    new CommitBranchService(branchNameService, new CommitBranchRules(branchNameService)),
                    stages
                ),
                new TimedBranchHierarchy(new BranchHierarchyService(), stages)
            )
        );
    }

    class StageTimes
    {
        readonly Dictionary<string, List<double>> times = [];

        public void Clear() => times.Clear();

        public void Time(string stage, Action run)
        {
            var stopwatch = Stopwatch.StartNew();
            run();
            if (!times.TryGetValue(stage, out var list))
                times[stage] = list = [];
            list.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        public IEnumerable<(string, double)> Medians(int runs) =>
            times.Select(p => (p.Key, p.Value.OrderBy(ms => ms).ElementAt(runs / 2)));
    }

    class TimedCommitGraph : ICommitGraphService
    {
        readonly ICommitGraphService inner;
        readonly StageTimes stages;

        public TimedCommitGraph(ICommitGraphService inner, StageTimes stages)
        {
            this.inner = inner;
            this.stages = stages;
        }

        public void SetGitBranchTipsOnCommits(WorkRepo repo) =>
            stages.Time("SetGitBranchTipsOnCommits", () => inner.SetGitBranchTipsOnCommits(repo));

        public void SetCommitParentsAndChildren(WorkRepo repo) =>
            stages.Time("SetCommitParentsAndChildren", () => inner.SetCommitParentsAndChildren(repo));
    }

    class TimedCommitBranch : ICommitBranchService
    {
        readonly ICommitBranchService inner;
        readonly StageTimes stages;

        public TimedCommitBranch(ICommitBranchService inner, StageTimes stages)
        {
            this.inner = inner;
            this.stages = stages;
        }

        public void DetermineAllCommitsBranches(WorkRepo repo, GitRepo gitRepo) =>
            stages.Time("DetermineAllCommitsBranches", () => inner.DetermineAllCommitsBranches(repo, gitRepo));
    }

    class TimedBranchHierarchy : IBranchHierarchyService
    {
        readonly IBranchHierarchyService inner;
        readonly StageTimes stages;

        public TimedBranchHierarchy(IBranchHierarchyService inner, StageTimes stages)
        {
            this.inner = inner;
            this.stages = stages;
        }

        public void DetermineBranchHierarchy(WorkRepo repo) =>
            stages.Time("DetermineBranchHierarchy", () => inner.DetermineBranchHierarchy(repo));

        public void DetermineRootBranch(WorkRepo repo) =>
            stages.Time("DetermineRootBranch", () => inner.DetermineRootBranch(repo));

        public void DetermineAncestors(WorkRepo repo) =>
            stages.Time("DetermineAncestors", () => inner.DetermineAncestors(repo));
    }
}
