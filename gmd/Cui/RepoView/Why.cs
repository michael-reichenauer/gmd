using gmd.Server;

namespace gmd.Cui.RepoView;

// Why a menu item is greyed out, for the reasons many items share. Said on the status line when a
// greyed out item is picked anyway, see MenuItem.WhyNot. The rest are said where the item is built,
// since they are about that item alone.
static class Why
{
    // What most commands that change branches need first: git refuses to switch, merge, pull or
    // rebase over changes it would have to overwrite, and gmd asks for a clean tree before a push
    public const string Changes = "Commit or stash the changes first";

    // A branch gmd knows only from the message of the merge that brought it in, drawn '~'
    public static string Deleted(Branch b) => $"'{b.NiceNameUnique}' was deleted: gmd knows it from a merge message";

    // Git lets a branch be checked out in one worktree only, and moves it only there
    public static string InWorktree(Branch b) => $"'{b.NiceNameUnique}' is checked out in another worktree";
}
