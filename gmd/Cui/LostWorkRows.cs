using System.Globalization;
using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui;

// The rows of the lost commits dialog, kept apart from the dialog so they can be asserted without a
// driver. One row per line of work, newest first, with the older versions of work that is still
// there last and dark:
//
//     Time            Branch              Commits  Lost by   Subject
//     24-10-15 12:03  feature                   2  deleted   Feature work
//     24-10-15 12:01  main                      1  amend     Second
static class LostWorkRows
{
    const int TimeWidth = 16;
    const int BranchWidth = 20;
    const int CommitsWidth = 9;
    const int LostByWidth = 10;
    const int FixedWidth = TimeWidth + BranchWidth + CommitsWidth + LostByWidth;

    // Narrower than this and the subject column has no room to say anything
    public const int MinWidth = FixedWidth + 20;

    public static Text Header(int width) =>
        Text.Dark("Time".Max(TimeWidth, true))
            .Dark("Branch".Max(BranchWidth, true))
            .Dark("Commits".Max(CommitsWidth - 2, true) + "  ")
            .Dark("Lost by".Max(LostByWidth, true))
            .Dark("Subject".Max(Math.Max(0, width - FixedWidth), true))
            .ToText();

    public static Text Row(LostWork work, int width)
    {
        var time = work.Time.ToString("yy-MM-dd HH:mm", CultureInfo.InvariantCulture).Max(TimeWidth, true);
        var branch = (work.BranchName != "" ? work.BranchName : "-").Max(BranchWidth - 1, true) + " ";
        var commits = $"{work.CommitIds.Count}".PadLeft(CommitsWidth - 2) + "  ";
        var lostBy = LostByName(work.LostBy).Max(LostByWidth, true);
        var subject = work.Subject.Max(Math.Max(0, width - FixedWidth), true);

        // An older version of work that is still there is listed for completeness, but dark
        if (work.IsRewritten)
            return Text.Dark(time + branch + commits + lostBy + subject).ToText();

        return Text.Dark(time).Cyan(branch).White(commits).Yellow(lostBy).White(subject).ToText();
    }

    // What the row cannot show: the tip, who made it, and what became of it in a sentence
    public static string Reason(LostWork work)
    {
        var tip = $"{work.TipId.Sid()} by {work.Author}";
        if (work.IsRewritten)
            return $"{tip}: an older version of commits still there, as an amend or a rebase leaves";

        var on = work.BranchName != "" ? $"'{work.BranchName}'" : "a branch";
        return work.LostBy switch
        {
            LostBy.Deleted => $"{tip}: made on {on}, which was deleted",
            LostBy.Detached => $"{tip}: made on a detached HEAD, left when a branch was checked out",
            LostBy.Reset => $"{tip}: left behind on {on} by a reset",
            LostBy.Rebase => $"{tip}: replaced on {on} by a rebase",
            LostBy.Amend => $"{tip}: replaced on {on} by an amend",
            LostBy.Pull => $"{tip}: replaced on {on} by a pull",
            _ => tip,
        };
    }

    static string LostByName(LostBy lostBy) =>
        lostBy switch
        {
            LostBy.Reset => "reset",
            LostBy.Rebase => "rebase",
            LostBy.Amend => "amend",
            LostBy.Pull => "pull",
            LostBy.Deleted => "deleted",
            LostBy.Detached => "detached",
            _ => "",
        };
}
