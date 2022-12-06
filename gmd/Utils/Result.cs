using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Reflection;

namespace gmd.Utils;


public static class Result
{
    public static bool Try<T>(
        [NotNullWhen(true)] out T? value,
        [NotNullWhen(false)] out ErrorResult? e,
        R<T> result)
    {
        return R.Try(out value, out e, result);
    }

    public static bool Try<T>(
       [NotNullWhen(true)] out T? value,
       R<T> result)
    {
        return R.Try(out value, result);
    }

    public static bool Try(
        [NotNullWhen(false)] out ErrorResult? e,
         R result)
    {
        return R.Try(out e, result);
    }

    public static bool Try(R result)
    {
        return R.Try(result);
    }
}


public class R
{
    protected static readonly Exception NoError = new Exception("No error");
    protected static readonly Exception NoValueError = new Exception("No value");

    protected R(Exception e)
    {
        resultException = e;
    }

    public static R Ok = Error(NoError);

    public static bool Try<T>(
     [NotNullWhen(true)] out T? value,
     [NotNullWhen(false)] out ErrorResult? e,
     R<T> result)
    {
        if (result.IsResultError)
        {
            value = default;
            e = result.GetResultError();
            return false;
        }

        value = result.GetResultValue()!;
        e = default;
        return true;
    }

    public static bool Try<T>(
       [NotNullWhen(true)] out T? value,
       R<T> result)
    {
        if (result.IsResultError)
        {
            value = default;
            return false;
        }

        value = result.GetResultValue()!;
        return true;
    }

    public static bool Try([NotNullWhen(false)] out ErrorResult? e, R result)
    {
        if (result.IsResultError)
        {
            e = result.GetResultError();
            return false;
        }

        e = default;
        return true;
    }

    public static bool Try(R result)
    {
        if (result.IsResultError)
        {
            return false;
        }

        return true;
    }

    public static ErrorResult Error(
          string message = "",
          [CallerMemberName] string memberName = "",
          [CallerFilePath] string sourceFilePath = "",
          [CallerLineNumber] int sourceLineNumber = 0) =>
          new ErrorResult(new Exception(message), memberName, sourceFilePath, sourceLineNumber);


    public static ErrorResult Error(
        string message,
        Exception e,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0) =>
        new ErrorResult(new Exception(message, e), memberName, sourceFilePath, sourceLineNumber);

    public static ErrorResult Error(
          string message,
          R errorResult,
          [CallerMemberName] string memberName = "",
          [CallerFilePath] string sourceFilePath = "",
          [CallerLineNumber] int sourceLineNumber = 0) =>
          new ErrorResult(new Exception(message, errorResult.GetResultException()), memberName, sourceFilePath, sourceLineNumber);


    public static ErrorResult Error(
           R errorResult,
           [CallerMemberName] string memberName = "",
           [CallerFilePath] string sourceFilePath = "",
           [CallerLineNumber] int sourceLineNumber = 0) =>
           errorResult.IsResultError ?
           new ErrorResult(errorResult.GetResultException(), memberName, sourceFilePath, sourceLineNumber) : throw Asserter.FailFast("Was no error error");

    public static ErrorResult Error(
        Exception e,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0) =>
        new ErrorResult(e, memberName, sourceFilePath, sourceLineNumber);


    internal string ErrorMessage => IsResultError ? resultException.Message : throw Asserter.FailFast("Result was not an error");
    internal ErrorResult GetResultError() => IsResultError ? Error(resultException) : throw Asserter.FailFast("Result was not an error");
    internal Exception GetResultException() => resultException;

    public static implicit operator R(Exception e) => Error(e);
    public static implicit operator bool(R r) => r.IsOk;
    public override string ToString() => IsOk ? "OK" : $"Error: {resultException.Message}";
    public string ToString(bool includeStack) => IsOk ? "OK" : $"Error: {AllErrorMessages()}\n{resultException}";


    internal bool IsResultError
    {
        get
        {
            isErrorChecked = true;
            return resultException != NoError;
        }
    }


    protected Exception resultException;
    // protected Error Error => IsResultError ? Error(resultException) : throw Asserter.FailFast("Result was not an error");
    protected bool IsOk => !IsResultError;
    protected bool isErrorChecked = false;
    internal string AllErrorMessages() => string.Join(",\n", AllMessageLines());


    private IEnumerable<string> AllMessageLines()
    {
        yield return resultException.Message;

        Exception? inner = resultException.InnerException;
        while (inner != null)
        {
            yield return inner.Message;
            inner = inner.InnerException;
        }
    }
}


public class R<T> : R
{
    private readonly T? storedValue = default;

    protected R(T value) : base(NoError) => this.storedValue = value;

    protected R(Exception error) : base(error) { }

    public T GetResultValue() => isErrorChecked ?
           IsOk ? storedValue! : throw Asserter.FailFast(resultException.ToString()) :
           throw Asserter.FailFast("IsError or IsOk was never checked");


    public T Or(T defaultValue) => IsResultError ? defaultValue : GetResultValue();

    public override string ToString() => IsOk ? (storedValue?.ToString() ?? "") : base.ToString();


    public static implicit operator R<T>(Exception e) => new R<T>(e);
    public static implicit operator R<T>(ErrorResult error) => new R<T>(error.GetResultException());
    public static implicit operator bool(R<T> r) => r.IsOk;

    public static implicit operator R<T>(T value)
    {
        if (value == null)
        {
            throw Asserter.FailFast("Value cannot be null");
        }

        return new R<T>(value);
    }
}



public class ErrorResult : R
{
    internal ErrorResult(Exception e, string memberName, string sourceFilePath, int sourceLineNumber)
         : base(AddStackTrace(e, ToStackTrace(memberName, sourceFilePath, sourceLineNumber)))
    {
    }

    private ErrorResult(Exception e, string stackTrace)
        : base(AddStackTrace(e, stackTrace))
    {
    }


    private static string ToStackTrace(string memberName, string sourceFilePath, int sourceLineNumber) =>
        $"at {sourceFilePath}({sourceLineNumber}){memberName}";

    private static Exception AddStackTrace(Exception exception, string stackTrace)
    {
        if (stackTrace == null)
        {
            return exception;
        }

        FieldInfo? field = typeof(Exception).GetField(
            "_remoteStackTraceString", BindingFlags.Instance | BindingFlags.NonPublic);

        string? stack = (string?)field?.GetValue(exception);
        stackTrace = string.IsNullOrEmpty(stack) ? stackTrace : $"{stackTrace}\n{stack}";
        field?.SetValue(exception, stackTrace);
        return exception;
    }
}
