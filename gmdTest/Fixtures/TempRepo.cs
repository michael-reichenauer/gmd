using gmd.Git;
using gmd.Git.Private;
using IOPath = System.IO.Path;

namespace gmdTest.Fixtures;

// TempRepo is a throwaway git repository in a temp folder, driven through the real git
// executable and the real IGit services. It is what the FakeCmd tests cannot be: a canary for
// git version and output format drift, since nothing here is canned.
//
// Nothing outside the temp folder is ever touched. The repository is created in the system temp
// folder, every git command runs with that folder as its working directory, and Dispose refuses
// to delete a path it did not create.
//
// The repository is also isolated from the developer's global git config: the settings that
// would change what these tests see (user, signing, hooks, line endings) are set locally.
//
// Use like e.g.:
//     using var repo = await TempRepo.CreateAsync();
//     await repo.CommitFileAsync("file.txt", "text", "Initial");
//     Assert.IsTrue(Try(out var log, out var e, await repo.Git.GetLogAsync(100, repo.Path)), $"{e}");
sealed class TempRepo : IDisposable
{
    // Both the temp folder name and the guard in Dispose, i.e. only folders named like this
    // are ever deleted
    const string FolderPrefix = "gmdTest-repo-";

    // Who commits, set locally below and passed explicitly by the pinned commits
    public const string AuthorName = "Test User";
    public const string AuthorEmail = "test@example.com";

    readonly ICmd cmd = new Cmd();

    string originPath = "";

    TempRepo(string path)
    {
        Path = path;
        Git = NewGit(cmd);
    }

    // The repository working folder, i.e. the 'wd' every IGit method takes
    public string Path { get; }

    // The real git services over the real git executable
    public IGit Git { get; }

    // Creates a new repository with 'main' as its initial branch and no commits yet
    public static async Task<TempRepo> CreateAsync()
    {
        var path = IOPath.Join(IOPath.GetTempPath(), $"{FolderPrefix}{Guid.NewGuid():N}");
        var repo = new TempRepo(path);
        await repo.InitAsync();
        return repo;
    }

    // The real git services, wired by hand rather than by the DI container
    public static IGit NewGit(ICmd cmd)
    {
        var logService = new LogService(cmd);
        var diffService = new DiffService(cmd);
        var remoteService = new RemoteService(cmd);

        return new gmd.Git.Private.Git(
            logService,
            new BranchService(cmd),
            new StatusService(cmd),
            new CommitService(cmd),
            diffService,
            remoteService,
            new RepoService(cmd),
            new TagService(cmd, remoteService),
            new KeyValueService(cmd),
            new StashService(cmd, logService, diffService),
            new BlameService(cmd),
            new ConflictService(cmd),
            cmd
        );
    }

    // Runs a raw git command in the repository, for the few setup steps IGit has no method for.
    // Fails the test if git does.
    public async Task<string> GitAsync(string args)
    {
        var result = await cmd.RunAsync("git", args, Path);
        Assert.AreEqual(0, result.ExitCode, $"'git {args}' failed:\n{result.ErrorOutput}");
        return result.Output;
    }

    // As GitAsync, but for the commands that are expected to fail — creating a conflict means
    // running a 'git merge' or 'git rebase' that stops, and those exit non-zero.
    public async Task<string> GitAllowFailAsync(string args) => (await cmd.RunAsync("git", args, Path)).Output;

    public void WriteFile(string name, string text) => File.WriteAllText(IOPath.Join(Path, name), text);

    public void DeleteFile(string name) => File.Delete(IOPath.Join(Path, name));

    // Commits all changes in the working folder and returns the id of the new commit
    public async Task<string> CommitAsync(string message)
    {
        Assert.IsTrue(Try(out var e, await Git.CommitAllChangesAsync(message, false, Path)), $"Commit failed: {e}");
        return await HeadIdAsync();
    }

    // Writes a file and commits it, returns the id of the new commit
    public async Task<string> CommitFileAsync(string name, string text, string message)
    {
        WriteFile(name, text);
        return await CommitAsync(message);
    }

    // The base time for pinned commits. Fixed so repeated runs produce identical repositories,
    // and the same value RepoBuilder uses, so the two fixture styles read alike.
    public static readonly DateTimeOffset BaseTime = new(2024, 10, 15, 12, 0, 0, TimeSpan.Zero);

    // Commits with the author and committer dates pinned, as well as the identity. That makes
    // three things deterministic which otherwise are not:
    //
    //   - the time column the UI draws, since it is the author time
    //   - the commit ids, since a commit object is exactly its tree, parents, identity, dates
    //     and message, so fixing all of those fixes its sha
    //   - the order of the rows, since 'git log --all --date-order' orders by commit date and
    //     has nothing left to break a tie with
    //
    // The last one is the reason this is not merely cosmetic. Times must be distinct and
    // increasing, for the same reason RepoBuilder's are.
    //
    // This goes around IGit rather than through it because ICmd cannot pass environment
    // variables, and the committer date can only be set by one: there is no git config and no
    // command line flag for it.
    public async Task<string> CommitAtAsync(string message, DateTimeOffset time)
    {
        Proc.Ok("git", ["add", "-A"], Path, PinnedEnv(time));
        Proc.Ok("git", ["commit", "-m", message, "--no-verify", "--no-gpg-sign"], Path, PinnedEnv(time));
        return await HeadIdAsync();
    }

    // Writes a file and commits it with the dates pinned, returns the id of the new commit
    public async Task<string> CommitFileAtAsync(string name, string text, string message, DateTimeOffset time)
    {
        WriteFile(name, text);
        return await CommitAtAsync(message, time);
    }

    // Runs a raw git command with the dates pinned, for the steps that create a commit without
    // 'git commit', i.e. a merge
    public string GitAt(string[] args, DateTimeOffset time) => Proc.Ok("git", args, Path, PinnedEnv(time));

    // A time as git reads it in GIT_AUTHOR_DATE / GIT_COMMITTER_DATE. Also used by TmuxSession, to
    // pin the dates of a commit the running gmd makes itself.
    public static string GitDate(DateTimeOffset time) => time.ToString("yyyy-MM-ddTHH:mm:sszzz");

    // The identity and dates a pinned commit is made with. The identity duplicates what InitAsync
    // sets locally, since the commit id depends on it and this leaves no doubt.
    static IReadOnlyDictionary<string, string> PinnedEnv(DateTimeOffset time)
    {
        var date = GitDate(time);
        return new Dictionary<string, string>
        {
            ["GIT_AUTHOR_DATE"] = date,
            ["GIT_COMMITTER_DATE"] = date,
            ["GIT_AUTHOR_NAME"] = AuthorName,
            ["GIT_AUTHOR_EMAIL"] = AuthorEmail,
            ["GIT_COMMITTER_NAME"] = AuthorName,
            ["GIT_COMMITTER_EMAIL"] = AuthorEmail,
        };
    }

    public async Task<string> HeadIdAsync() => (await GitAsync("rev-parse HEAD")).Trim();

    // Adds a bare repository next to this one as its 'origin' remote, so pushing, fetching and
    // the ahead/behind counts of a tracking branch can be tested without a network.
    public async Task AddOriginAsync()
    {
        originPath = Path + "-origin";
        var result = await cmd.RunAsync("git", $"init --bare \"{originPath}\"", "");
        Assert.AreEqual(0, result.ExitCode, $"'git init --bare' failed:\n{result.ErrorOutput}");

        await GitAsync($"remote add origin \"{originPath}\"");
    }

    public void Dispose()
    {
        DeleteFolder(Path);
        if (originPath != "")
            DeleteFolder(originPath);
    }

    async Task InitAsync()
    {
        Assert.IsTrue(Try(out var e, await Git.InitRepoAsync(Path, "")), $"Init failed: {e}");

        // The initial branch name is a user setting (init.defaultBranch), so it is named
        // explicitly to keep the fixture identical on every machine. HEAD is unborn at this
        // point, so this only names the branch the first commit will create.
        await GitAsync("symbolic-ref HEAD refs/heads/main");

        // Local config, so the developer's global config cannot change what the tests see: who
        // commits, hooks being run, line endings being rewritten, conflict markers gaining a
        // base section (diff3/zdiff3), diff headers losing their 'a/' 'b/' prefixes, and a
        // global ignore file hiding a file the test just wrote. The last two paths are files
        // that do not exist, i.e. no hooks and nothing ignored.
        await GitAsync($"config user.name \"{AuthorName}\"");
        await GitAsync($"config user.email {AuthorEmail}");
        await GitAsync("config commit.gpgsign false");
        await GitAsync("config tag.gpgsign false");
        await GitAsync("config core.autocrlf false");
        await GitAsync("config core.hooksPath no-hooks");
        await GitAsync("config core.excludesFile no-excludes");
        await GitAsync("config merge.conflictStyle merge");
        await GitAsync("config diff.noprefix false");
        await GitAsync("config diff.mnemonicPrefix false");
    }

    // Deletes a folder this fixture created, and only such a folder, so a test can never delete
    // a working tree by mistake
    static void DeleteFolder(string path)
    {
        if (!path.StartsWith(IOPath.GetTempPath()) || !IOPath.GetFileName(path).StartsWith(FolderPrefix))
            throw new InvalidOperationException($"Refusing to delete '{path}', it is not a temp repo folder");

        if (!Directory.Exists(path))
            return;

        // Git marks files in the object store read only, which blocks deleting them on Windows
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);

        // A failed cleanup should not fail a test, the folder is in temp and will be cleaned by
        // the system eventually
        if (!Try(out var e, () => Directory.Delete(path, true)))
            Log.Warn($"Failed to delete temp repo '{path}', {e}");
    }
}
