using System.Diagnostics.CodeAnalysis;

namespace gmd.Utils;

public static class Result
{
    public static bool Try<T>(
        [NotNullWhen(true)] out T? value,
        [NotNullWhen(false)] out Error? e,
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

    public static bool Try([NotNullWhen(false)] out Error? e, R result)
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
}


public class R
{
    protected static readonly Exception NoError = new Exception("No error");
    protected static readonly Exception NoValueError = new Exception("No value");

    public static R Ok = new Error(NoError);
    public static Error NoValue = new Error(NoValueError);

    public Error GetResultError() => IsResultError ? Error.From(resultException) : throw Asserter.FailFast("Result was not an error");
    public Exception GetResultException() => resultException;

    public static implicit operator R(Exception e) => new Error(e);
    public static implicit operator bool(R r) => r.IsOk;
    public override string ToString() => IsOk ? "OK" : $"Error: {resultException}";
    public string ToString(bool includeStack) => IsOk ? "OK" : $"Error: {AllErrorMessages()}\n{resultException}";


    protected R(Exception e)
    {
        resultException = e;
    }


    public bool IsResultError
    {
        get
        {
            isErrorChecked = true;
            return resultException != NoError;
        }
    }


    protected Exception resultException;
    protected Error Error => IsResultError ? Error.From(resultException) : throw Asserter.FailFast("Result was not an error");
    protected bool IsOk => !IsResultError;
    protected bool isErrorChecked = false;
    protected string AllErrorMessages() => string.Join(",\n", AllMessageLines());


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

    public new static readonly R<T> NoValue = new R<T>(NoValueError);

    private R(T value) : base(NoError) => this.storedValue = value;

    private R(Exception error) : base(error) { }

    public T GetResultValue() => isErrorChecked ?
           IsOk ? storedValue! : throw Asserter.FailFast(resultException.ToString()) :
           throw Asserter.FailFast("IsError or IsOk was never checked");


    public T Or(T defaultValue) => IsResultError ? defaultValue : GetResultValue();

    public override string ToString() => IsOk ? (storedValue?.ToString() ?? "") : base.ToString();


    public static implicit operator R<T>(Exception e) => new R<T>(e);
    public static implicit operator R<T>(Error error) => new R<T>(error.GetResultException());
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
