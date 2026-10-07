using System.Globalization;
using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui;

// The rows of the deleted branches dialog, kept apart from the dialog so they can be asserted without
// a driver, and what the status line says of a delete and a restore. One row per branch gmd deleted,
// newest first, with the side or sides it was deleted on:
//
//     Time            Branch              Deleted        Subject
//     24-10-15 12:03  feature             local, remote  Feature work
//     24-10-15 12:01  fix                 local          Fix the login
static class DeletedBranchRows
{
    const int TimeWidth = 16;
    const int BranchWidth = 20;
    const int SidesWidth = 15;
    const int FixedWidth = TimeWidth + BranchWidth + SidesWidth;

    // Narrower than this and the subject column has no room to say anything
    public const int MinWidth = FixedWidth + 20;

    public static Text Header(int width) =>
        Text.Dark("Time".Max(TimeWidth, true))
            .Dark("Branch".Max(BranchWidth, true))
            .Dark("Deleted".Max(SidesWidth, true))
            .Dark("Subject".Max(Math.Max(0, width - FixedWidth), true))
            .ToText();

    public static Text Row(DeletedBranch deleted, int width)
    {
        var time = deleted.Time.ToString("yy-MM-dd HH:mm", CultureInfo.InvariantCulture).Max(TimeWidth, true);
        var branch = deleted.Name.Max(BranchWidth - 1, true) + " ";
        var sides = Sides(deleted).Max(SidesWidth, true);
        var subject = deleted.Subject.Max(Math.Max(0, width - FixedWidth), true);

        return Text.Dark(time).Cyan(branch).Yellow(sides).White(subject).ToText();
    }

    // What Restore does with the selected branch, which the row cannot show
    public static string Reason(DeletedBranch deleted)
    {
        var local = $"creates '{deleted.Name}' at {deleted.TipId.Sid()} again";
        var remote = $"pushes '{deleted.RemoteName}' back to {deleted.RemoteTipId.Sid()}";
        return (deleted.IsLocal, deleted.IsRemote) switch
        {
            (true, true) => $"Restore {local}, and {remote}",
            (true, false) => $"Restore {local}",
            _ => $"Restore {remote}",
        };
    }

    // What the status line says once a branch is deleted, here or on origin or both, i.e. that it can
    // be brought back and where from
    public static string Deleted(string name, bool isBoth) =>
        $"Deleted '{name}'{(isBoth ? " here and on origin" : "")}: Restore Deleted Branch in the Undo menu brings it back";

    // What the status line says while a restore runs, and once it has
    public static string Restoring(DeletedBranch deleted, bool isLocal) =>
        $"Restoring '{(isLocal ? deleted.Name : deleted.RemoteName)}'";

    public static string Restored(DeletedBranch deleted, bool isLocal, bool isRemote) =>
        (isLocal, isRemote) switch
        {
            (true, true) => $"Restored '{deleted.Name}' here and on origin",
            (true, false) => $"Restored '{deleted.Name}'",
            _ => $"Restored '{deleted.RemoteName}'",
        };

    static string Sides(DeletedBranch deleted) =>
        (deleted.IsLocal, deleted.IsRemote) switch
        {
            (true, true) => "local, remote",
            (true, false) => "local",
            _ => "remote",
        };
}
