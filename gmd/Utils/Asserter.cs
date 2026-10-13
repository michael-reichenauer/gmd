using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace gmd.Utils;

// Asserts an invariant of gmd's own. A broken one is a bug, so it stops gmd: Fail reports it, to
// ExceptionHandling, before it is thrown, so that it stops gmd even where a catch takes the
// exception for an ordinary failure, and in a test, where nothing listens, it is only thrown.
//
//   _ => throw Asserter.Fail($"Unknown kind {kind}"),
//   Asserter.Requires(index >= 0);
public static class Asserter
{
    // Raised for each broken invariant, before it is thrown
    public static event Action<AssertException>? Failed;

    public static void Requires(
        bool predicate,
        [CallerArgumentExpression(nameof(predicate))] string expression = "",
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
    {
        if (!predicate)
            throw Fail($"Requires {expression}", memberName, sourceFilePath, sourceLineNumber);
    }

    // The exception to throw, which an arm of a switch expression can throw as well as a statement
    public static AssertException Fail(
        string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
    {
        var exception = new AssertException(
            $"{message}, at {sourceFilePath}({sourceLineNumber}) {memberName}",
            new StackTrace(1, true)
        );
        Failed?.Invoke(exception);
        return exception;
    }
}

// A broken invariant of gmd's own, i.e. a bug, see Asserter. Result.Catch and IsNotFatal let it
// through, since it is not a failure of what they guard.
public sealed class AssertException : Exception
{
    readonly StackTrace createdAt;

    public AssertException(string message, StackTrace createdAt)
        : base(message) => this.createdAt = createdAt;

    // Where it was created until it is thrown, since it is reported, and logged, before that
    public override string? StackTrace => base.StackTrace ?? createdAt.ToString();
}
