using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace gmd.Utils;

// Every fallible operation returns an R or an R<T>: a union of its value (or Success) and an Error,
// matched on the case type. Exceptions are for bugs, not for flow control.
//
//   var result = await git.GetStatusAsync(wd);
//   if (result is not Status status) return result.Error;
//   ... status is a Status from here on
//
//   if (await git.SetValueAsync(key, json, wd) is Error e) return e;
//
//   return await server.PullAsync(name, wd) switch
//   {
//       Success => R.Ok,
//       Error e => new Error("Failed to pull", e),
//   };
//
//   var tags = await git.GetTagsAsync(wd) is IReadOnlyList<Tag> t ? t : [];
//
// R and R<T> are custom unions in the C# 15 sense: a struct with the [Union] attribute, one public
// constructor per case type and an object Value. So a switch over one is exhaustive without a
// discard arm (a missing arm is a build error), and a pattern applies to the contained value rather
// than to the struct. The attribute is polyfilled while the target framework is net10.0, see
// UnionPolyfill.cs.

// The failure case of R and R<T>: a message, where it was created, and optionally the error or the
// exception it wraps.
public class Error
{
    public Error(
        string message = "",
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
        : this(message, null, null, memberName, sourceFilePath, sourceLineNumber) { }

    public Error(
        string message,
        Error inner,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
        : this(message, inner, null, memberName, sourceFilePath, sourceLineNumber) { }

    public Error(
        string message,
        Exception exception,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
        : this(message, null, exception, memberName, sourceFilePath, sourceLineNumber) { }

    public Error(
        Exception exception,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
        : this(exception.Message, null, exception, memberName, sourceFilePath, sourceLineNumber) { }

    Error(
        string message,
        Error? inner,
        Exception? exception,
        string memberName,
        string sourceFilePath,
        int sourceLineNumber
    )
    {
        Message = message;
        Inner = inner;
        Exception = exception;
        Origin = $"{sourceFilePath}({sourceLineNumber}) {memberName}";
    }

    public string Message { get; }

    // The error this one wraps, if any
    public Error? Inner { get; }

    // The exception this one wraps, if any
    public Exception? Exception { get; }

    // The file, line and member that created the error, since nothing is thrown and so there is no
    // stack trace to tell
    public string Origin { get; }

    // This message and every wrapped one, outermost first, which is what a dialog shows
    public string AllMessages() => string.Join(",\n", Messages());

    IEnumerable<string> Messages()
    {
        yield return Message;

        if (Inner != null)
        {
            foreach (var message in Inner.Messages())
                yield return message;
        }

        // An error created from an exception already has that exception's message as its own
        var exception = Exception;
        if (exception != null && exception.Message == Message)
            exception = exception.InnerException;

        for (; exception != null; exception = exception.InnerException)
            yield return exception.Message;
    }

    public override string ToString() => $"Error: {Message}";
}

// The success case of R
public sealed class Success
{
    public static readonly Success Instance = new();

    Success() { }

    public override string ToString() => "OK";
}

// The result of an operation that either succeeds or fails: Success or Error
[Union]
public readonly struct R : IUnion
{
    readonly object? value;

    public R(Success success) => value = success;

    public R(Error error) => value = error;

    public static readonly R Ok = new(Success.Instance);

    public object? Value => value;

    public bool HasValue => value is not null;

    public bool TryGetValue([MaybeNullWhen(false)] out Success success)
    {
        success = value as Success;
        return success is not null;
    }

    public bool TryGetValue([MaybeNullWhen(false)] out Error error)
    {
        error = value as Error;
        return error is not null;
    }

    public static implicit operator R(Error error) => new(error);

    // Runs an action that reports failure by throwing, e.g. a file API, and returns the exception
    // as an error. Every exception is caught, the fatal ones included, since bad input to such an
    // API surfaces as an ArgumentException or an InvalidOperationException.
    //
    //   if (R.Catch(() => File.Move(source, target)) is Error e) return e;
    public static R Catch(
        Action action,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
    {
        try
        {
            action();
            return Ok;
        }
        catch (Exception e)
        {
            return new Error(e, memberName, sourceFilePath, sourceLineNumber);
        }
    }

    // Runs a function that reports failure by throwing and returns its value, or the exception as
    // an error.
    //
    //   var text = R.Catch(() => File.ReadAllText(path));
    //   if (text is not string content) return text.Error;
    public static R<T> Catch<T>(
        Func<T> func,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
        where T : notnull
    {
        try
        {
            return func();
        }
        catch (Exception e)
        {
            return new Error(e, memberName, sourceFilePath, sourceLineNumber);
        }
    }

    public override string ToString() =>
        value switch
        {
            Error e => e.ToString(),
            Success => "OK",
            _ => "Unset",
        };
}

// The result of an operation that either produces a value or fails: T or Error
[Union]
public readonly struct R<T> : IUnion
    where T : notnull
{
    readonly object? value;

    // A null value is not an error, it is a bug in the function returning it
    public R(T value) => this.value = value ?? throw Asserter.FailFast("Value cannot be null");

    public R(Error error) => value = error;

    public object? Value => value;

    public bool HasValue => value is not null;

    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        if (this.value is T t)
        {
            value = t;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetValue([MaybeNullWhen(false)] out Error error)
    {
        error = value as Error;
        return error is not null;
    }

    // The error of a result already known to be one, i.e. right after a pattern ruled out the
    // value; reading it on a value is a bug in the caller:
    //
    //   if (result is not Status status) return result.Error;
    public Error Error => value as Error ?? throw Asserter.FailFast("Result is not an error");

    public static implicit operator R<T>(T value) => new(value);

    public static implicit operator R<T>(Error error) => new(error);

    // Dropping the value keeps the outcome
    public static implicit operator R(R<T> result) => result.value is Error e ? new R(e) : R.Ok;

    public override string ToString() =>
        value switch
        {
            Error e => e.ToString(),
            null => "Unset",
            _ => value.ToString() ?? "",
        };
}
