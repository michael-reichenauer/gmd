namespace gmd.Git.Private;

// What git is part way through, i.e. what it stopped and has to be told to finish or abort, and
// how far it has got. Read from the files git leaves in the git dir, by the status and by the
// commands that act on the operation; no porcelain command reports it.
record OperationState(
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
    public static readonly OperationState None = new(GitOperation.None, "", "", "", 0, 0, true);

    // True while git is part way through an operation it has to be told to finish or abort. What
    // this guards is the commit's 'git add .' and 'git commit -a': either would stage an unmerged
    // path with the conflict markers as its content and drop the stages, which cannot be undone.
    // The uncommitted diff needs no guard, since it stages into a copy of the index.
    public bool IsInProgress => Operation != GitOperation.None;

    // A merge git left no MERGE_HEAD for, which 'merge --abort' cannot undo — it answers "There is
    // no merge to abort (MERGE_HEAD missing)". That is the state gmd's own Cherry Pick leaves
    // behind when it conflicts: 'cherry-pick --no-commit' writes only MERGE_MSG, no operation ref
    // of any kind, so there is nothing for a '--abort' verb to attach to. Also a squash merge.
    public bool IsMergeWithoutHead => Operation == GitOperation.Merge && MergeHeadId == "";

    // Which operation git stopped part way through, probed from the files it leaves in the git dir.
    // No porcelain command reports this, so file probing is also what git's own wt_status_get_state()
    // does.
    //
    // The order matters, and MERGE_MSG being last is the whole point: a stopped rebase and a stopped
    // cherry-pick both write it, so it never meant "a merge is in progress" — which is exactly the
    // assumption that let 'git add .' run during a rebase and destroy the conflict.
    public static OperationState Read(string wd)
    {
        var gitDir = GitDir.GitDirPathOf(wd);
        if (gitDir == "")
            return None;

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
            return new OperationState(
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
            return new OperationState(
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
            return new OperationState(
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
            return new OperationState(
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
            return new OperationState(
                GitOperation.Merge,
                message,
                ReadFirstLine(Path_("MERGE_HEAD")),
                "",
                0,
                0,
                IsFinishedByCommit: true
            );

        return None;
    }

    static string ReadFirstLine(string path) =>
        Result.Catch(() => File.ReadAllText(path)) is string text ? text.Split('\n')[0].Trim() : "";

    static int ReadNumber(string path) => int.TryParse(ReadFirstLine(path), out var value) ? value : 0;

    static string TrimRefsHeads(string name) => name.TrimPrefix("refs/heads/");
}
