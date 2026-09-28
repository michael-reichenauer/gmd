using System.Runtime.CompilerServices;

namespace gmd.Git;

// A git command that stopped on conflicts: a merge, rebase, cherry pick, revert or pull that left
// the repository part way through the operation, for the conflicts to be resolved and the
// operation then finished or aborted. It is an error to the command that ran it, but the UI does
// not show it as one: it shows what stopped, the files, and the way on (RepoCommands.ShowConflicts).
class ConflictError : Error
{
    public ConflictError(
        string message,
        Error inner,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
        : base(message, inner, memberName, sourceFilePath, sourceLineNumber) { }

    // Whether the error, or one it wraps, is a conflict: a command wraps what failed under it in an
    // error of its own ("Failed to merge ..."), so the conflict is often further down
    public static bool IsIn(Error error) => error is ConflictError || (error.Inner is Error inner && IsIn(inner));

    // The conflict of a git command that printed one, or the result as it was
    public static Result ToConflict(Result result, string message) =>
        result is CmdError e && e.Output.Contains("CONFLICT") ? new ConflictError(message, e) : result;
}
