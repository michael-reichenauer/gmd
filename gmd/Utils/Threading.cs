namespace gmd.Utils;

static class Threading
{
    static int mainThreadId = Thread.CurrentThread.ManagedThreadId;
    static SynchronizationContext sctx = SynchronizationContext.Current!;

    public static int CurrentId => Thread.CurrentThread.ManagedThreadId;

    internal static void SetUp()
    {
        mainThreadId = Thread.CurrentThread.ManagedThreadId;
        sctx = SynchronizationContext.Current!;
    }

    internal static void PostOnMain(Action action)
    {
        sctx.Post((o) => action(), null);
    }

    internal static void AssertIsMainThread()
    {
        if (CurrentId > mainThreadId)
        {
            Asserter.FailFast($"Current thread {CurrentId} != {mainThreadId}");
        }
    }

    internal static void AssertIsOtherThread()
    {
        if (CurrentId == mainThreadId)
        {
            Asserter.FailFast($"Current thread {CurrentId} == {mainThreadId}");
        }
    }
}