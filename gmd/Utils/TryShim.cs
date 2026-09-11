using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace gmd.Utils;

// TEMPORARY. The Try helpers of the former result type, over the union R/R<T> in Result.cs, so that
// the tree compiles while their call sites are moved to pattern matching one layer at a time. This
// file, and the 'global using static gmd.Utils.Result' in the Usings.cs of both projects, go in the
// last step of that work. Do not add call sites.
public static class Result
{
    public static bool Try<T>([NotNullWhen(true)] out T? value, [NotNullWhen(false)] out Error? e, R<T> result)
        where T : notnull
    {
        if (result.TryGetValue(out value))
        {
            e = null;
            return true;
        }

        if (result.TryGetValue(out e))
        {
            value = default;
            return false;
        }

        throw Asserter.FailFast("Result has no value");
    }

    public static bool Try([NotNullWhen(false)] out Error? e, R result)
    {
        if (result is Success)
        {
            e = null;
            return true;
        }

        if (result.TryGetValue(out e))
            return false;

        throw Asserter.FailFast("Result has no value");
    }

    public static bool Try<T>([NotNullWhen(true)] out T? value, R<T> result)
        where T : notnull => Try(out value, out _, result);

    public static bool Try(R result) => Try(out _, result);

    public static bool Try<T>(
        [NotNullWhen(true)] out T? value,
        [NotNullWhen(false)] out Error? e,
        Func<T> func,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
        where T : notnull => Try(out value, out e, R.Catch(func, memberName, sourceFilePath, sourceLineNumber));

    public static bool Try(
        [NotNullWhen(false)] out Error? e,
        Action action,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    ) => Try(out e, R.Catch(action, memberName, sourceFilePath, sourceLineNumber));
}
