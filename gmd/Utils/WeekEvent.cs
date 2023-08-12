namespace gmd.Utils;

class WeekEvent<T>
{
    List<WeakReference> registered = new List<WeakReference>();

    public void Add(Action<T> action) => registered.Add(new WeakReference(action));

    public void Raise(T value)
    {
        var isCleanNeeded = false;

        registered.ForEach(wr =>
        {
            Action<T>? action = wr.Target as Action<T>;
            if (action == null) isCleanNeeded = true;
            action?.Invoke(value);
        });

        if (isCleanNeeded) registered = registered.Where(wr => wr.Target != null).ToList();
    }
}