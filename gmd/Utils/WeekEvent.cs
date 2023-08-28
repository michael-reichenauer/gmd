namespace gmd.Utils;


class WeekEvent
{
    List<WeakReference<Action>> registered = new List<WeakReference<Action>>();

    public void Add(Action action) => registered.Add(new WeakReference<Action>(action));

    public void Raise()
    {
        var isCleanNeeded = false;
        registered.ForEach(wr =>
        {
            if (!wr.TryGetTarget(out var action))
            {
                isCleanNeeded = true;
            }

            action?.Invoke();
        });

        if (isCleanNeeded) registered = registered.Where(wr => wr.TryGetTarget(out var _)).ToList();
    }
}

class WeekEvent<T>
{
    List<WeakReference<Action<T>>> registered = new List<WeakReference<Action<T>>>();

    public void Add(Action<T> action) => registered.Add(new WeakReference<Action<T>>(action));

    public void Raise(T value)
    {
        var isCleanNeeded = false;
        registered.ForEach(wr =>
        {
            if (!wr.TryGetTarget(out var action))
            {
                isCleanNeeded = true;
            }

            action?.Invoke(value);
        });

        if (isCleanNeeded) registered = registered.Where(wr => wr.TryGetTarget(out var _)).ToList();
    }
}