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
        await GitAsync("config user.name \"Test User\"");
        await GitAsync("config user.email test@example.com");
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
