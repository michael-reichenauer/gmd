using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;


namespace gmd.Utils;

public class R
{
    protected static readonly Exception NoError = new Exception("No error");
    protected static readonly Exception NoValueError = new Exception("No value");

    public static R Ok = new Error(NoError);
    public static Error NoValue = new Error(NoValueError);


    protected R(Exception e)
    {
        Exception = e;
    }

    public Exception Exception { get; }

    public Error Error => IsError ? Error.From(Exception) : throw Asserter.FailFast("Was no error error");
    public bool IsOk => !IsError;
    protected bool isErrorChecked = false;
    public bool IsError
    {
        get
        {
            isErrorChecked = true;
            return Exception != NoError;
        }
    }

    public string Message => Exception.Message;
    public string AllMessages => string.Join(",\n", AllMessageLines());


    public static R<T> From<T>(T result) => R<T>.From(result);



    //public static implicit operator R(Exception e) => new RError(e);
    public static implicit operator bool(R r) => r.IsOk;

    public override string ToString() => IsOk ? "OK" : $"Error: {AllMessages}\n{Exception}";

    private IEnumerable<string> AllMessageLines()
    {
        yield return Message;

        Exception? inner = Exception.InnerException;
        while (inner != null)
        {
            yield return inner.Message;
            inner = inner.InnerException;
        }
    }
}


public class R<T> : R
{
    private readonly T storedValue;

    public new static readonly R<T> NoValue = new R<T>(NoValueError);

    private R(T value) : base(NoError) => this.storedValue = value;

#pragma warning disable CS8618 
    private R(Exception error) : base(error) { }
#pragma warning restore CS8618

    //public static implicit operator R<T>(Error error) => new R<T>(error);
    //public static implicit operator R<T>(Exception e) => new R<T>(e);

    public static implicit operator R<T>(Error error) => new R<T>(error.Exception);
    public static implicit operator bool(R<T> r) => r.IsOk;

    public static implicit operator R<T>(T value)
    {
        if (value == null)
        {
            throw Asserter.FailFast("Value cannot be null");
        }

        return new R<T>(value);
    }

    public static R<T> From(T result) => new R<T>(result);

    public T Value => isErrorChecked ?
        IsOk ? storedValue : throw Asserter.FailFast(Exception.ToString()) :
        throw Asserter.FailFast("IsError or IsOk was never checked");

    public bool HasValue(out T value)
    {
        if (IsOk)
        {
            value = storedValue;
            return true;
        }
        else
        {
            value = storedValue;
            return false;
        }
    }

    public T Or(T defaultValue) => IsError ? defaultValue : Value;


    public override string ToString() => IsOk ? (storedValue?.ToString() ?? "") : base.ToString();
}
