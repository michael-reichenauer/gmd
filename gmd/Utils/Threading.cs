namespace gmd.Utils;

static class Threading
{
    static int mainThreadId = Thread.CurrentThread.ManagedThreadId;

    static int CurrentId => Thread.CurrentThread.ManagedThreadId;

    internal static void SetUp()
    {
        mainThreadId = Thread.CurrentThread.ManagedThreadId;
    }

    internal static void AssertIsMainThread()
    {
        if (CurrentId > mainThreadId)
        {
            Asserter.FailFast($"Current thread {CurrentId} != {mainThreadId}");
        }
    }
}
