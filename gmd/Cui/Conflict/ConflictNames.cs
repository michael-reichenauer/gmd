using System.Text.RegularExpressions;
using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.Conflict;

// What the resolver calls the two sides of a conflict. Git writes 'HEAD' and a branch into the
// markers, or a commit and its subject during a rebase, and HEAD is named here by the branch it is
// on, the name a user knows it by; and each side's title says what it was doing, by the subject of
// its commit. Ours and Theirs are the names, said in the keys and the menu, the titles head the
// columns. No view, so it is unit testable.
record ConflictNames(string Ours, Text OursTitle, string Theirs, Text TheirsTitle)
{
    public static ConflictNames Of(ConflictFile file, ConflictSides sides)
    {
        var hunk = file.Hunks.FirstOrDefault();
        var (oursLabel, theirsLabel) = hunk != null ? (hunk.OursLabel, hunk.TheirsLabel) : ("ours", "theirs");

        var isOnBranch = oursLabel == "HEAD" && sides.OursBranch != "";
        var ours = isOnBranch ? sides.OursBranch : oursLabel;
        var oursTitle = Text.Yellow(ours).Dark(isOnBranch ? " (HEAD)" : "").Add(Subject(sides.OursSubject));

        // A rebase or a cherry pick writes the commit and its subject, 'abc1234 (Subject)', and a
        // revert the parent of the commit reverted, 'parent of abc1234 (Subject)'
        var commit = Regex.Match(theirsLabel, @"^((?:parent of )?\S+) \((.*)\)$");
        var (theirs, theirsSubject) = commit.Success
            ? (commit.Groups[1].Value, commit.Groups[2].Value)
            : (theirsLabel, sides.TheirsSubject);
        var theirsTitle = Text.Cyan(theirs).Add(Subject(theirsSubject));

        return new ConflictNames(ours, oursTitle, theirs, theirsTitle);
    }

    static Text Subject(string subject) => subject == "" ? Text.Empty : Text.Dark($" · {subject}");
}
