namespace gmdTest.Fixtures;

// The repository the end-to-end UI tests are written against: small enough that a whole screen
// fits in a snapshot, and interesting enough that the graph has something to draw — a branch that
// is merged back, a tag, and a branch that is hidden by default so its only trace is the dark
// more-markers the show/hide feature leaves behind.
//
// Every commit has its author and committer date pinned to a distinct, increasing minute, so the
// time column, the commit ids and the row order are the same on every machine. Note what that
// costs: change a message, a file or a date and the id of that commit and of every commit after
// it changes with it, so the snapshots asserting them have to be regenerated.
//
// There is deliberately no origin remote. RepoView fetches when a repo is opened and every five
// minutes after that, with no way to turn it off, so the cheapest deterministic answer is to give
// it nothing to fetch from. It also keeps the ahead/behind markers out of every snapshot.
//
// The working tree is left clean, so there is no uncommitted row — that row is the one thing on
// the screen whose time is DateTime.Now rather than a pinned commit date. CreateWithChangesAsync
// is the variant that does have one, for the tests about committing.
static class E2eRepo
{
    // The standard shape:
    //
    //     main   Add delta            (tag v1.0)      12:06
    //     main   Merge branch 'dev' into main         12:05
    //     main   Add gamma                            12:04
    //     dev      More dev work                      12:03
    //     dev      Work on dev                        12:02
    //     main   Add beta                             12:01
    //     main   Initial                              12:00
    public static async Task<TempRepo> CreateAsync()
    {
        var repo = await TempRepo.CreateAsync();
        var t = TempRepo.BaseTime;

        await repo.CommitFileAtAsync("alpha.txt", "alpha\n", "Initial", t);
        await repo.CommitFileAtAsync("beta.txt", "beta gamma delta\n", "Add beta", t.AddMinutes(1));

        await repo.GitAsync("checkout -q -b dev");
        await repo.CommitFileAtAsync("dev.txt", "one\n", "Work on dev", t.AddMinutes(2));
        await repo.CommitFileAtAsync("dev.txt", "one\ntwo\n", "More dev work", t.AddMinutes(3));

        await repo.GitAsync("checkout -q main");
        await repo.CommitFileAtAsync("gamma.txt", "gamma\n", "Add gamma", t.AddMinutes(4));

        repo.GitAt(["merge", "--no-ff", "-m", "Merge branch 'dev' into main", "dev"], t.AddMinutes(5));

        await repo.CommitFileAtAsync("delta.txt", "delta\n", "Add delta", t.AddMinutes(6));
        await repo.GitAsync("tag v1.0");

        return repo;
    }

    // The same shape with uncommitted changes on top: one tracked file modified and one new file
    // added, so the log view draws the uncommitted row, the application bar the change count, and
    // the commit dialog has two changes to report.
    //
    // Both kinds are there on purpose: gmd commits by running 'git add .' and then
    // 'git commit -am' (CommitService), and the add is what picks up an untracked file, so a
    // fixture with only modified files would not notice if it were lost.
    public static async Task<TempRepo> CreateWithChangesAsync()
    {
        var repo = await CreateAsync();

        repo.WriteFile("alpha.txt", "alpha\nchanged\n");
        repo.WriteFile("epsilon.txt", "epsilon\n");

        return repo;
    }

    // The same shape with an 'origin' remote next door, everything pushed to it except one commit
    // on top. That last part is the point: amend is only offered for a commit that is still ahead
    // of the remote (CommitCommands.Commit checks CurrentCommit().IsAhead), i.e. one that has not
    // been published yet and can still be rewritten.
    //
    // Note that this is the only fixture here with a remote, so it is also the only one whose
    // screens carry the ahead marker and the local/remote branch tips.
    public static async Task<TempRepo> CreateWithOriginAsync()
    {
        var repo = await CreateAsync();

        await repo.AddOriginAsync();
        await repo.GitAsync("push -q --set-upstream origin main");

        // The tag has to be pushed as well, and not for tidiness: gmd fetches with --prune-tags
        // (RemoteService.cs), which deletes local tags that are not on the remote, so a local only
        // tag disappears from this fixture at whatever moment the first fetch completes. Pushing
        // it is what keeps these screens stable. See the finding in MODERNIZATION.md, Step 12.
        await repo.GitAsync("push -q origin v1.0");

        await repo.CommitFileAtAsync("zeta.txt", "zeta\n", "Add zeta", TempRepo.BaseTime.AddMinutes(7));

        return repo;
    }

    // A repository with more commits than fit on a screen, for the scrolling tests
    public static async Task<TempRepo> CreateLongAsync(int commits = 30)
    {
        var repo = await TempRepo.CreateAsync();

        for (int i = 0; i < commits; i++)
        {
            await repo.CommitFileAtAsync(
                "file.txt",
                $"line {i}\n",
                $"Commit number {i:00}",
                TempRepo.BaseTime.AddMinutes(i)
            );
        }

        return repo;
    }
}
