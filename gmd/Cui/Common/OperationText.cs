using gmd.Server;

namespace gmd.Cui.Common;

// What an operation git stopped part way through is called wherever gmd names it: the repo menu's
// items that act on it, the box saying it stopped on conflicts, the resolver's header and its box
// once they are resolved. They had a name each, 'Apply Patches', 'Apply patches' and 'Patch'.
static class OperationText
{
    // e.g. "Rebase", and "Operation" when nothing is in progress
    public static string Name(GitOperation operation) =>
        operation switch
        {
            GitOperation.Merge => "Merge",
            GitOperation.CherryPick => "Cherry Pick",
            GitOperation.Revert => "Revert",
            GitOperation.Rebase => "Rebase",
            GitOperation.Am => "Apply Patches",
            _ => "Operation",
        };
}
