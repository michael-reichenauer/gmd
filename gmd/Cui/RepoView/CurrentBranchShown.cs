using gmd.Server;

namespace gmd.Cui.RepoView;

// The branch the user is on is shown when it becomes current, however it became current: switched
// to in gmd, which shows it itself, or checked out in a terminal or by another tool, e.g. Claude Code,
// which gmd only learns of by reading the repo again; and when gmd opens a repo or a worktree on it.
// Otherwise a branch made and checked out outside gmd stays hidden, and the log does not show the
// branch the user is working on.
//
// Only when it *becomes* current, so that a user who hides it afterwards is not overruled by the next
// refresh. That is why the last current branch shown is remembered rather than compared with the
// repo shown before: a rebase or a checkout of a commit goes through a detached HEAD and back, and
// the branch it comes back to has not become current, it has stayed so. Nor is it a show the user
// asked for, so it is not recorded for undo (ShownHistory).
//
// This is state with no view, so it is tested without a terminal; RepoView owns it, asks it after
// each read and tells it of each repo shown.
class CurrentBranchShown
{
    string path = "";
    string name = "";

    // The current branch to show in a repo just read, or "" when there is none to show: it is not
    // the one last shown as current, and it is hidden
    public string ToShow(Repo repo)
    {
        var current = CurrentOf(repo);
        if (current == null || (repo.Path == path && current.Name == name))
            return "";

        return current.IsInView ? "" : current.Name;
    }

    // Remembers the current branch of a repo that is shown, whether or not it is in view, since the
    // user may have hidden it
    public void Shown(Repo repo)
    {
        var current = CurrentOf(repo);
        if (current == null)
            return;

        (path, name) = (repo.Path, current.Name);
    }

    // A search's results are not the branches the user has shown, and a detached HEAD is always
    // shown, so neither has a current branch to show or to remember
    static Branch? CurrentOf(Repo repo) =>
        repo.Filter != "" ? null : repo.AllBranches.FirstOrDefault(b => b.IsCurrent && !b.IsDetached);
}
