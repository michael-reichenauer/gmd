namespace gmd.Git.Private;

interface IStatusService
{
    Task<R<Status>> GetStatusAsync(string wd);
}

class StatusService : IStatusService
{
    private readonly ICmd cmd;

    public StatusService(ICmd cmd)
    {
        this.cmd = cmd;
    }

    public async Task<R<Status>> GetStatusAsync(string wd)
    {
        var args = "status -s --porcelain --ahead-behind --untracked-files=all";
        if (!Try(out var output, out var e, await cmd.RunAsync("git", args, wd)))
            return e;

        return Parse(output, wd);
    }

    private R<Status> Parse(string statusText, string wd)
    {
        var lines = statusText.Split('\n');

        int conflicted = 0;
        List<ConflictedFile> conflicts = [];
        int added = 0;
        List<string> addedFiles = [];
        int deleted = 0;
        List<string> deletedFiles = [];
        int modified = 0;
        List<string> modifiedFiles = [];
        int renamed = 0;
        List<string> renamedSourceFiles = [];
        List<string> renamedTargetFiles = [];

        foreach (var lineText in lines)
        {
            string line = lineText.Trim();
            if (line == "")
            {
                continue;
            }

            // The seven unmerged codes. The kind is kept rather than discarded: it is the only
            // record of what git could not merge, and it decides what can be offered for the path
            // (a modify/delete has no text to merge, only a keep-or-delete choice).
            if (ToConflictKind(line) is ConflictKind kind)
            {
                conflicted++;
                conflicts.Add(new ConflictedFile(line.Substring(3).Trim().Replace("\"", ""), kind));
            }
            else if (line.StartsWith("?? ") || line.StartsWith(" A "))
            {
                added++;
                addedFiles.Add(line.Substring(3).Trim().Replace("\"", ""));
            }
            else if (line.StartsWith("D"))
            {
                deleted++;
                deletedFiles.Add(line.Substring(2).Trim().Replace("\"", ""));
            }
            else if (line.StartsWith("R"))
            {
                renamed++;
                var parts = line.Substring(2).Split(" -> ");
                renamedSourceFiles.Add(parts[0].Trim().Replace("\"", ""));
                renamedTargetFiles.Add(parts[1].Trim().Replace("\"", ""));
            }
            else
            {
                modified++;
                modifiedFiles.Add(line.Substring(2).Trim().Replace("\"", ""));
            }
        }
        var operation = GetOperationStatus(wd);

        return new Status(
            modified,
            added,
            deleted,
            conflicted,
            renamed,
            operation.Operation,
            operation.MergeMessage,
            operation.MergeHeadId,
            operation.BranchName,
            operation.Step,
            operation.Total,
            operation.IsFinishedByCommit,
            modifiedFiles.ToArray(),
            addedFiles.ToArray(),
            deletedFiles.ToArray(),
            conflicts.ToArray(),
            renamedSourceFiles.ToArray(),
            renamedTargetFiles.ToArray()
        );
    }

    // The XY code of an unmerged path, or null if the line is not one. The space at index 2 is part
    // of the test, as the 'UU ' prefixes it replaces were, so the Substring(3) below is always the
    // path. Note the line has already been trimmed, which is why a staged add ('A  x') reaches the
    // modified branch instead — see the note on TestParseCounts.
    static ConflictKind? ToConflictKind(string line) =>
        line.Length > 3 && line[2] == ' ' ? ToConflictKind(line[0], line[1]) : null;

    static ConflictKind? ToConflictKind(char x, char y) =>
        (x, y) switch
        {
            ('U', 'U') => ConflictKind.BothModified,
            ('A', 'A') => ConflictKind.BothAdded,
            ('D', 'D') => ConflictKind.BothDeleted,
            ('A', 'U') => ConflictKind.AddedByUs,
            ('U', 'A') => ConflictKind.AddedByThem,
            ('U', 'D') => ConflictKind.DeletedByThem,
            ('D', 'U') => ConflictKind.DeletedByUs,
            _ => null,
        };

    record OperationStatus(
        GitOperation Operation,
        string MergeMessage,
        string MergeHeadId,
        string BranchName,
        int Step,
        int Total,
        // Whether making a commit is the whole of what is left, rather than '--continue'. See the
        // note on Status.IsFinishedByCommit.
        bool IsFinishedByCommit
    )
    {
        public static readonly OperationStatus None = new(GitOperation.None, "", "", "", 0, 0, true);
    }

    // Which operation git stopped part way through, probed from the files it leaves in the git dir.
    // No porcelain command reports this, so file probing is also what git's own wt_status_get_state()
    // does.
    //
    // The order matters, and MERGE_MSG being last is the whole point: a stopped rebase and a stopped
    // cherry-pick both write it, so it never meant "a merge is in progress" — which is exactly the
    // assumption that let 'git add .' run during a rebase and destroy the conflict.
    static OperationStatus GetOperationStatus(string wd)
    {
        var gitDir = GetGitDir(wd);
        if (gitDir == "")
            return OperationStatus.None;

        string Path_(params string[] parts) => Path.Join([gitDir, .. parts]);
        var message = ReadFirstLine(Path_("MERGE_MSG"));

        // A rebase with the merge backend, which has been the default since git 2.26.
        //
        // Interactive is not told apart from plain: modern git runs both through the sequencer and
        // writes 'rebase-merge/interactive' for both, so the file no longer distinguishes them
        // (verified — a plain 'git rebase main' produces it). Nothing here needs the difference
        // anyway, since --continue, --skip and --abort are the same commands either way.
        if (Directory.Exists(Path_("rebase-merge")))
        {
            return new OperationStatus(
                GitOperation.Rebase,
                message,
                "",
                TrimRefsHeads(ReadFirstLine(Path_("rebase-merge", "head-name"))),
                ReadNumber(Path_("rebase-merge", "msgnum")),
                ReadNumber(Path_("rebase-merge", "end")),
                IsFinishedByCommit: false
            );
        }

        // 'git am' and a rebase with the --apply backend share a directory; only the 'applying'
        // file tells them apart. Neither writes MERGE_MSG, which is what made them invisible.
        if (Directory.Exists(Path_("rebase-apply")))
        {
            var operation = File.Exists(Path_("rebase-apply", "applying")) ? GitOperation.Am : GitOperation.Rebase;
            return new OperationStatus(
                operation,
                message,
                "",
                TrimRefsHeads(ReadFirstLine(Path_("rebase-apply", "head-name"))),
                ReadNumber(Path_("rebase-apply", "next")),
                ReadNumber(Path_("rebase-apply", "last")),
                IsFinishedByCommit: false
            );
        }

        // A cherry pick, which is always one started outside gmd: gmd's own runs '--no-commit',
        // and that writes no CHERRY_PICK_HEAD at all, so it never reaches here. Whatever did write
        // one is git driving a sequence, which '--continue' is how to carry on with.
        if (File.Exists(Path_("CHERRY_PICK_HEAD")))
            return new OperationStatus(
                GitOperation.CherryPick,
                message,
                ReadFirstLine(Path_("CHERRY_PICK_HEAD")),
                "",
                0,
                0,
                IsFinishedByCommit: false
            );

        // A revert, where the same is not true: 'revert --no-commit' *does* record REVERT_HEAD,
        // and it records it even when the revert applies cleanly — so gmd's own Undo/Revert Commit
        // leaves a state that looks exactly like a stopped one, with a change staged for the commit
        // dialog and nothing to continue. The sequencer todo is what tells them apart, git writing
        // it only for a revert of several commits, which is the form where a commit really would
        // make one of them and leave the rest unapplied.
        if (File.Exists(Path_("REVERT_HEAD")))
            return new OperationStatus(
                GitOperation.Revert,
                message,
                ReadFirstLine(Path_("REVERT_HEAD")),
                "",
                0,
                0,
                IsFinishedByCommit: !Directory.Exists(Path_("sequencer"))
            );

        // A merge, finally. MERGE_MSG rather than MERGE_HEAD, since a squash merge or a merge of an
        // unrelated ref writes only the message.
        if (message != "")
            return new OperationStatus(
                GitOperation.Merge,
                message,
                ReadFirstLine(Path_("MERGE_HEAD")),
                "",
                0,
                0,
                IsFinishedByCommit: true
            );

        return OperationStatus.None;
    }

    // The real git dir of the working folder. Usually '<wd>/.git', but in a linked worktree and in
    // a submodule '.git' is a *file* holding 'gitdir: <path>' — and that is where the operation
    // state lives, so joining '.git' blindly would find none of it there. Empty if there is none.
    internal static string GetGitDir(string wd) =>
        Try(out var gitDir, out var _, GitDir.Resolve(wd)) ? gitDir.GitDirPath : "";

    static string ReadFirstLine(string path) =>
        Try(out var text, out var _, () => File.ReadAllText(path)) ? text.Split('\n')[0].Trim() : "";

    static int ReadNumber(string path) => int.TryParse(ReadFirstLine(path), out var value) ? value : 0;

    static string TrimRefsHeads(string name) => name.TrimPrefix("refs/heads/");

    // Which operation git is part way through, for the callers that only need that much: the two
    // 'git add .' guards below, and ConflictService deciding what '--abort' attaches to.
    internal static GitOperation GetOperation(string wd) => GetOperationStatus(wd).Operation;

    // Whether committing is what finishes the operation in progress, rather than '--continue'.
    // See the note on Status.IsFinishedByCommit.
    internal static bool IsFinishedByCommit(string wd) => GetOperationStatus(wd).IsFinishedByCommit;

    // A merge git left no MERGE_HEAD for, which 'merge --abort' cannot undo — it answers "There is
    // no merge to abort (MERGE_HEAD missing)". That is the state gmd's own Cherry Pick leaves
    // behind when it conflicts: 'cherry-pick --no-commit' writes only MERGE_MSG, no operation ref
    // of any kind, so there is nothing for a '--abort' verb to attach to. Also a squash merge.
    internal static bool IsMergeWithoutHead(string wd)
    {
        var operation = GetOperationStatus(wd);
        return operation.Operation == GitOperation.Merge && operation.MergeHeadId == "";
    }

    // True while git is part way through an operation it has to be told to finish or abort. What
    // this guards is 'git add .' and 'git commit -a': either would stage an unmerged path with the
    // conflict markers as its content and drop the stages, which cannot be undone.
    internal static bool IsOperationInProgress(string wd) => GetOperation(wd) != GitOperation.None;
}
