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
// the screen whose time is DateTime.Now rather than a pinned commit date.
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
