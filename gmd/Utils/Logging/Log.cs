using System.Runtime.CompilerServices;

namespace gmd.Utils.Logging;

static class Log
{
    const string LevelDebug = "DEBUG";
    const string LevelInfo = "INFO ";
    const string LevelWarn = "WARN ";
    const string LevelError = "ERROR";

    public static void Debug(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
    {
        Write(LevelDebug, msg, memberName, sourceFilePath, sourceLineNumber);
    }

    public static void Info(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
    {
        Write(LevelInfo, msg, memberName, sourceFilePath, sourceLineNumber);
    }

    public static void Warn(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
    {
        Write(LevelWarn, msg, memberName, sourceFilePath, sourceLineNumber);
    }

    public static void Error(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
    {
        Write(LevelError, msg, memberName, sourceFilePath, sourceLineNumber);
    }

    public static void Exception(
        Exception e,
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
    {
        Write(LevelError, $"{msg}\n{e}", memberName, sourceFilePath, sourceLineNumber);
    }

    private static void Write(string level, string msg, string memberName, string sourceFilePath, int sourceLineNumber)
    {
        LogFile.Write(level, msg, memberName, sourceFilePath, sourceLineNumber);
    }
}
