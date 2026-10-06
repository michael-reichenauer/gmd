using gmd.Server;

namespace gmd.Cui.RepoView;

// What Undo says for the last change of a branch (Repo.UndoSteps): the menu item's name, why it
// cannot be picked, and what the status line says while and after it runs. Plain functions of the
// repo, so they are testable without a view.
static class BranchUndo
{
    // A subject is cut to this, so that the name of the menu item fits beside the others
    const int MaxNameLength = 30;

    // The last change of a branch, by its local name, since a remote branch is undone as its local
    // branch, and null when there is none
    public static UndoStep? StepOf(Repo repo, Branch branch)
    {
        var name = branch.IsRemote ? branch.LocalName : branch.Name;
        return name != "" && repo.UndoSteps.TryGetValue(name, out var step) ? step : null;
    }

    // The menu item, e.g. "Undo Rebase", "Undo Commit 'Fix the login'" or "Redo Merge of 'feature'"
    public static string Label(UndoStep? step) =>
        step == null ? "Undo Last Change" : $"{(step.IsRedo ? "Redo" : "Undo")} {Title(step)}";

    // Why Undo cannot act, or "" when it can. A branch checked out elsewhere is moved only there, and
    // the current branch cannot be reset in the middle of a merge or a rebase, nor, for a change
    // whose files go back too, with uncommitted changes git would have to overwrite or refuse over.
    // A commit already pushed is not undone, as Uncommit does not: it would come back with a pull.
    public static string WhyNot(Repo repo, Branch branch, UndoStep? step)
    {
        if (step == null || !repo.BranchByName.TryGetValue(step.BranchName, out var local))
            return $"Nothing to undo on '{branch.NiceNameUnique}'";
        if (local.WorktreePath != "")
            return Why.InWorktree(local);
        if (local.IsCurrent && repo.Status.IsMerging)
            return Why.InProgress;
        if (local.IsCurrent && step.Mode == UndoMode.Keep && !repo.Status.IsOk)
            return Why.Changes;
        if (step.Kind is StepKind.Commit or StepKind.Amend && step.IsPushed && !step.IsRedo)
            return "Only a commit not yet pushed can be undone";
        return "";
    }

    // What the status line says while it runs, e.g. "Undoing the rebase of 'dev'"
    public static string Doing(UndoStep step) =>
        $"{(step.IsRedo ? "Redoing" : "Undoing")} {Phrase(step)} on '{step.BranchName}'";

    // What the status line says once it has run. An undo can be undone in turn, which redoes the
    // change; a change that is on origin is still there, since an undo never pushes.
    public static string Done(UndoStep step)
    {
        var done = $"{(step.IsRedo ? "Redid" : "Undid")} {Phrase(step)} on '{step.BranchName}'";
        if (step.IsRedo)
            return done;
        return step.IsPushed && step.Kind != StepKind.Pull
            ? $"{done}, origin still has it: Undo again redoes it"
            : $"{done}: Undo again redoes it";
    }

    static string Title(UndoStep step) =>
        step.Kind switch
        {
            StepKind.Commit => Named("Commit", step.Name),
            StepKind.Amend => Named("Amend", step.Name),
            StepKind.Merge => step.Name == "" ? "Merge" : $"Merge of {Quoted(step.Name)}",
            StepKind.CherryPick => Named("Cherry Pick", step.Name),
            StepKind.Revert => Named("Revert", step.Name),
            StepKind.Pull => "Pull",
            StepKind.Rebase => "Rebase",
            StepKind.Uncommit => "Uncommit",
            StepKind.Reset => "Reset",
            StepKind.Squash => "Squash",
            _ => "Last Change",
        };

    static string Phrase(UndoStep step) =>
        step.Kind switch
        {
            StepKind.Commit => Of("the commit", step.Name, ""),
            StepKind.Amend => Of("the amend", step.Name, "of "),
            StepKind.Merge => Of("the merge", step.Name, "of "),
            StepKind.CherryPick => Of("the cherry pick", step.Name, "of "),
            StepKind.Revert => Of("the revert", step.Name, "of "),
            StepKind.Pull => "the pull",
            StepKind.Rebase => "the rebase",
            StepKind.Uncommit => "the uncommit",
            StepKind.Reset => "the reset",
            StepKind.Squash => "the squash",
            _ => "the last change",
        };

    static string Named(string title, string name) => name == "" ? title : $"{title} {Quoted(name)}";

    static string Of(string phrase, string name, string of) => name == "" ? phrase : $"{phrase} {of}{Quoted(name)}";

    static string Quoted(string name) =>
        name.Length > MaxNameLength ? $"'{name[..(MaxNameLength - 1)]}…'" : $"'{name}'";
}
