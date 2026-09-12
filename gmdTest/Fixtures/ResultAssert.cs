namespace gmdTest.Fixtures;

// Assertions on Result and Result<T> that hand back the case they assert, so a test reads
//
//     var commits = AssertOk(await log.GetLogAsync(100, "/wd"));
//     var e = AssertError(await log.GetLogAsync(100, "/wd"));
//
// Available everywhere through the static global using in Usings.cs.
public static class ResultAssert
{
    // Matched on Value rather than 'result is T value': a union pattern whose type is a type
    // parameter cannot declare a variable, since the compiler cannot tell the struct from its contents
    public static T AssertOk<T>(Result<T> result, string message = "")
        where T : notnull
    {
        if (result.Value is T value)
            return value;
        throw new AssertFailedException($"Expected a value but got {result.Error.AllMessages()}{Suffix(message)}");
    }

    public static void AssertOk(Result result, string message = "")
    {
        if (result is Error e)
            throw new AssertFailedException($"Expected success but got {e.AllMessages()}{Suffix(message)}");
    }

    public static Error AssertError<T>(Result<T> result, string message = "")
        where T : notnull
    {
        if (result is Error e)
            return e;
        throw new AssertFailedException($"Expected an error but got the value {result}{Suffix(message)}");
    }

    public static Error AssertError(Result result, string message = "")
    {
        if (result is Error e)
            return e;
        throw new AssertFailedException($"Expected an error but got success{Suffix(message)}");
    }

    static string Suffix(string message) => message == "" ? "" : $": {message}";
}
