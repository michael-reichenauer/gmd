namespace gmd.Cui.Common;

enum StatusKind
{
    Info, // What a command did, e.g. "Pushed main"
    Notice, // Why a key did nothing, and what to do instead
    Failure, // Something that failed in the background, e.g. the fetch
}

record StatusMessage(string Text, StatusKind Kind, DateTime ShownAt);

// The one line message at the bottom of the log view, shown for a few seconds in place of the key
// hints: what a command did, or why a key did nothing. It replaces both the silence of a key that
// could not act and the red error box for what is not an error, which had to be dismissed.
//
// This is only the message and how long it is shown; the log view draws it, see KeyHintBar.
interface IStatusLine
{
    // The message to show now, or null when there is none or it has been shown long enough
    StatusMessage? Current { get; }

    // Raised when a message is shown, on the thread showing it
    event Action? Changed;

    void Info(string text);
    void Notice(string text);
    void Failure(string text);
}

[SingleInstance]
class StatusLine : IStatusLine
{
    internal static readonly TimeSpan Duration = TimeSpan.FromSeconds(5);

    StatusMessage? message;

    // For the tests, which cannot wait for the seconds to pass
    internal Func<DateTime> Now { get; init; } = () => DateTime.UtcNow;

    public event Action? Changed;

    public StatusMessage? Current => message != null && Now() - message.ShownAt < Duration ? message : null;

    public void Info(string text) => Show(text, StatusKind.Info);

    public void Notice(string text) => Show(text, StatusKind.Notice);

    public void Failure(string text) => Show(text, StatusKind.Failure);

    void Show(string text, StatusKind kind)
    {
        message = new StatusMessage(text, kind, Now());
        Changed?.Invoke();
    }
}

// A command that did not run, for a reason that is not a failure: nothing to push, changes to
// commit first. A command returns it as it would any error, and the command runner shows it on
// the status line rather than in an error box. It is its message only, never wrapping another.
class Notice : Error
{
    public Notice(
        string message,
        [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0
    )
        : base(message, memberName, sourceFilePath, sourceLineNumber) { }
}
