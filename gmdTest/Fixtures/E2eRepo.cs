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

    // The same shape with the changes already stashed, i.e. a clean working tree and one stash on
    // top of it. The stash is what puts the 'ß' on the commit it was made on and the count in the
    // application bar, and it is what the pop and drop menus have something to list.
    //
    // A stash is a commit, so it has a time of its own — but nothing draws it, only the marker and
    // the message, so there is no date to pin here.
    public static async Task<TempRepo> CreateWithStashAsync()
    {
        var repo = await CreateWithChangesAsync();

        await repo.GitAsync("stash push -u -m \"stashed work\"");

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

        // Note what is deliberately *not* done here: v1.0 is not pushed. It used to have to be,
        // since gmd fetched with --prune-tags, which deletes every local tag the remote does not
        // have — so the tag vanished from this fixture at whatever moment the first fetch completed
        // and the screens flaked. That is fixed (Step 17), and leaving the tag unpushed is what
        // keeps these screens honest about it: if unpushed tags are ever deleted again, the tag
        // disappears from the snapshots below.
        await repo.CommitFileAtAsync("zeta.txt", "zeta\n", "Add zeta", TempRepo.BaseTime.AddMinutes(7));

        return repo;
    }

    // The mirror of the above: everything is pushed and then the local branch is moved back a
    // commit, so origin has one the local branch has not got. That is what draws the behind marker
    // and gives 'Pull/Update' something to do.
    //
    // Moving the local branch back rather than committing on the remote is the cheap way to get
    // there: a bare repository has no working tree to commit in, and a second clone would be a
    // second temp folder to clean up.
    public static async Task<TempRepo> CreateBehindOriginAsync()
    {
        var repo = await CreateWithOriginAsync();

        await repo.GitAsync("push -q origin main");
        await repo.GitAsync("reset -q --hard HEAD~1");

        return repo;
    }

    // A branch that was never merged, with 'dev' checked out and 'main' one commit ahead of where
    // it branched off. Cherry-pick is only offered for a commit that is not on the current branch
    // (CommitMenu checks rb != cb), and 'main' is always shown whatever the current branch is, so
    // its commit is on screen and under the cursor without having to open a branch first.
    //
    //     main   Add gamma      12:02   <- not on dev, the one to pick
    //     dev    Work on dev    12:01   <- current
    //     main   Initial        12:00
    public static async Task<TempRepo> CreateWithUnmergedBranchAsync()
    {
        var repo = await TempRepo.CreateAsync();
        var t = TempRepo.BaseTime;

        await repo.CommitFileAtAsync("alpha.txt", "alpha\n", "Initial", t);

        await repo.GitAsync("checkout -q -b dev");
        await repo.CommitFileAtAsync("dev.txt", "one\n", "Work on dev", t.AddMinutes(1));

        await repo.GitAsync("checkout -q main");
        await repo.CommitFileAtAsync("gamma.txt", "gamma\n", "Add gamma", t.AddMinutes(2));

        await repo.GitAsync("checkout -q dev");

        return repo;
    }

    // Two commits changing one line in the middle of a 40 line file, plus a two line file beside
    // it. Every file of the fixtures above is one or two lines long, so the whole file is already
    // drawn at the default 6 lines of context and asking for more would change nothing on screen —
    // this is the fixture the diff view's '+' and '-' keys need. The short file is there to show
    // that widening one file leaves the others alone.
    //
    // Deliberately its own repository rather than another commit on CreateAsync: changing that
    // fixture would change the id of that commit and of every commit after it, and with it every
    // snapshot in TerminalTest that names one.
    public static async Task<TempRepo> CreateWithLongFileAsync()
    {
        var repo = await TempRepo.CreateAsync();
        var t = TempRepo.BaseTime;

        var lines = Enumerable.Range(1, 40).Select(i => $"line {i}").ToList();
        await repo.CommitFileAtAsync("long.txt", string.Join("\n", lines) + "\n", "Add long file", t);
        await repo.CommitFileAtAsync("short.txt", "one\ntwo\n", "Add short file", t.AddMinutes(1));

        // One commit touching both, so the diff has a file to widen and a file to leave alone
        lines[19] = "line 20 changed";
        repo.WriteFile("long.txt", string.Join("\n", lines) + "\n");
        repo.WriteFile("short.txt", "one\nTWO\n");
        await repo.CommitAtAsync("Change both files", t.AddMinutes(2));

        return repo;
    }

    // A merge stopped by a conflict, i.e. the state the conflict resolver is opened in. The one
    // conflict is in the middle of an 80 line file on purpose: it is then off the screen both from
    // the top of the file and from the bottom of it, so a resolver that opened at the top would not
    // be showing what it was opened for, and next/previous conflict have somewhere to move to.
    public static async Task<TempRepo> CreateWithConflictAsync()
    {
        var repo = await TempRepo.CreateAsync();
        var t = TempRepo.BaseTime;

        var lines = Enumerable.Range(1, 80).Select(i => $"line {i}").ToList();
        await repo.CommitFileAtAsync("long.txt", string.Join("\n", lines) + "\n", "Add long file", t);

        await repo.GitAsync("checkout -q -b dev");
        lines[39] = "line 40 on dev";
        await repo.CommitFileAtAsync("long.txt", string.Join("\n", lines) + "\n", "Change it on dev", t.AddMinutes(1));

        await repo.GitAsync("checkout -q main");
        lines[39] = "line 40 on main";
        await repo.CommitFileAtAsync("long.txt", string.Join("\n", lines) + "\n", "Change it on main", t.AddMinutes(2));

        // Fails, which is the whole point: it leaves the merge in progress with the file conflicted.
        // No date to pin, since a merge that stops on a conflict writes no commit.
        await repo.GitAllowFailAsync("merge dev");

        return repo;
    }

    // The same, with two conflicts far enough apart that git keeps them as two — for the tests
    // about resolving a file in more than one go, where one conflict cannot show anything
    public static async Task<TempRepo> CreateWithTwoConflictsAsync()
    {
        var repo = await TempRepo.CreateAsync();
        var t = TempRepo.BaseTime;

        var lines = Enumerable.Range(1, 80).Select(i => $"line {i}").ToList();
        await repo.CommitFileAtAsync("long.txt", string.Join("\n", lines) + "\n", "Add long file", t);

        await repo.GitAsync("checkout -q -b dev");
        lines[19] = "line 20 on dev";
        lines[59] = "line 60 on dev";
        await repo.CommitFileAtAsync("long.txt", string.Join("\n", lines) + "\n", "Change it on dev", t.AddMinutes(1));

        await repo.GitAsync("checkout -q main");
        lines[19] = "line 20 on main";
        lines[59] = "line 60 on main";
        await repo.CommitFileAtAsync("long.txt", string.Join("\n", lines) + "\n", "Change it on main", t.AddMinutes(2));

        await repo.GitAllowFailAsync("merge dev");

        return repo;
    }

    // Plain commits on a branch that is fully pushed, for squashing them together. Everything is
    // pushed on purpose: a commit belongs to the remote branch once it is on it, and squash refuses
    // a commit whose branch is not IsLocalCurrent — a flag only a remote branch ever carries.
    public static async Task<TempRepo> CreatePushedPlainCommitsAsync(int commits = 4)
    {
        var repo = await CreateLongAsync(commits);

        await repo.AddOriginAsync();
        await repo.GitAsync("push -q --set-upstream origin main");

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
