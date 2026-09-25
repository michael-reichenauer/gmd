namespace gmd.Cui.RepoView;

// The commits the last search found, for n and Shift-N to step through in the log once one of them
// was picked, rather than searching again for each: the log is where a match is seen in its place,
// among the commits around it, and the search list is not. The order is the search list's, i.e.
// the log's, so n goes down it and Shift-N up.
//
// It is set by picking a match, and cleared by a search that ends without one. It is kept per log
// view for the session, as ShownHistory is, and not saved.
class SearchMatches
{
    List<string> ids = [];

    public string Filter { get; private set; } = "";
    public int Count => ids.Count;
    public bool IsActive => ids.Count > 0;

    // Which one is shown, 1 based as it is said, 0 before any is
    public int Number { get; private set; }

    public void Set(string filter, IReadOnlyList<string> matchIds, string pickedId)
    {
        Filter = filter;
        ids = matchIds.ToList();
        Number = ids.IndexOf(pickedId) + 1;
    }

    public void Clear()
    {
        Filter = "";
        ids = [];
        Number = 0;
    }

    // The next match in the direction, 1 down and -1 up, which then is the one shown; null past the
    // last one in that direction, where the one shown stays as it was
    public string? Step(int direction)
    {
        var next = Number + direction;
        if (next < 1 || next > ids.Count)
            return null;

        Number = next;
        return ids[next - 1];
    }
}
