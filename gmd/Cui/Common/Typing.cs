namespace gmd.Cui.Common;

// Whether the user is typing into a text input, i.e. has edited it within the last Pause, and a
// callback for when they stop. A spell checked input leaves the word being typed alone while they
// type, and colors it once they pause, so that a misspelled last word, which the caret never
// leaves before Enter, is still seen before it is committed.
class Typing
{
    public static readonly TimeSpan Pause = TimeSpan.FromSeconds(1);

    readonly Action onPause;
    object? timeout;

    internal Typing(Action onPause)
    {
        this.onPause = onPause;
    }

    // Typing is a pause timer that has not yet fired. With no main loop there is no timer, and so
    // no typing either.
    public bool IsTyping => timeout != null;

    public void Edited()
    {
        if (timeout != null)
            UI.RemoveTimeout(timeout);

        timeout = UI.AddTimeout(
            Pause,
            () =>
            {
                timeout = null;
                onPause();
            }
        );
    }
}
