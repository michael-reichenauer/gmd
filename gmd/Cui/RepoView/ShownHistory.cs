namespace gmd.Cui.RepoView;

// A show or a hide of branches, and the branches shown before it, which undoing it goes back to.
// Verb and What are how it is named, e.g. "Hide" and "'feature'", and BranchName is the one branch
// it was about, if it was about one, so that undoing a hide can scroll to the branch it brings back.
record ShownChange(string Verb, string What, string BranchName, IReadOnlyList<string> Before)
{
    public bool IsHide => Verb == "Hide";

    public override string ToString() => $"{Verb} {What}";
}

// The branches shown before each show and hide, so that Backspace can go back to them one step at a
// time, the way a browser goes back a page. Which branches are shown is gmd's own state, not git's,
// so git has no way back to offer, and showing a few branches to look at something should not cost
// finding the way back to the tidy log by hand.
//
// Only what the user asked for is recorded. A branch shown as a side effect of another command, e.g.
// the one just created, is not something the key should hide again. Nor is a change that shows or
// hides nothing, e.g. showing a branch already shown, since undoing it would look like a dropped
// key. It is kept per log view for the session and not saved, and a new repo starts a new one.
//
// This is state with no view, so it is tested without a terminal; RepoView owns it and the branch
// commands record in it.
class ShownHistory
{
    // Enough to never be the limit in practice, while not growing without bound in a long session
    const int MaxCount = 100;

    readonly List<ShownChange> changes = [];

    // The change the next undo goes back from, null when there is none
    public ShownChange? Last => changes.Count > 0 ? changes[^1] : null;

    public void Add(
        IReadOnlyList<string> before,
        IReadOnlyList<string> after,
        string verb,
        string what,
        string name = ""
    )
    {
        if (before.ToHashSet().SetEquals(after))
            return;

        changes.Add(new ShownChange(verb, what, name, before));
        if (changes.Count > MaxCount)
            changes.RemoveAt(0);
    }

    // Takes the last change off, for going back to what was shown before it
    public ShownChange? Undo()
    {
        var last = Last;
        if (last != null)
            changes.RemoveAt(changes.Count - 1);
        return last;
    }

    public void Clear() => changes.Clear();
}
