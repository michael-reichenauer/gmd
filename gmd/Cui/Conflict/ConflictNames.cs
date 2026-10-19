using System.Text.RegularExpressions;
using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui.Conflict;

// What the resolver calls the two sides of a conflict. Git writes 'HEAD' and a branch into the
// markers, or a commit and its subject during a rebase, and HEAD is named here by the branch it is
// on, the name a user knows it by. A side named by a commit alone, HEAD on no branch or a commit's
// id, has the subject of its commit in its title, which says what it was doing; a branch's name says
// enough by itself, and its subject made the row long. Ours and Theirs are the names, said in the
// keys and the menu, the titles head the columns. No view, so it is unit testable.
record ConflictNames(string Ours, Text OursTitle, string Theirs, Text TheirsTitle)
{
    public static ConflictNames Of(ConflictFile file, ConflictSides sides)
    {
        var hunk = file.Hunks.FirstOrDefault();
        var (oursLabel, theirsLabel) = hunk != null ? (hunk.OursLabel, hunk.TheirsLabel) : ("ours", "theirs");

        // 'git checkout --merge', which puts a file's conflicts back (Unresolve), labels the sides 'ours'
        // and 'theirs', the very words the names are here to avoid, since during a rebase they mean the
        // opposite of what is expected: they are named as the operation's own markers name them
        if (oursLabel == "ours")
            oursLabel = "HEAD";
        if (theirsLabel == "theirs" && sides.TheirsName != "")
            theirsLabel = sides.TheirsName;

        var isOnBranch = oursLabel == "HEAD" && sides.OursBranch != "";
        var ours = isOnBranch ? sides.OursBranch : oursLabel;
        var oursTitle = isOnBranch
            ? Text.Yellow(ours).Dark(" (HEAD)")
            : Text.Yellow(ours).Add(Subject(ours, sides.OursSubject));

        // A rebase or a cherry pick writes the commit and its subject, 'abc1234 (Subject)', and a
        // revert the parent of the commit reverted, 'parent of abc1234 (Subject)'
        var commit = Regex.Match(theirsLabel, @"^((?:parent of )?\S+) \((.*)\)$");
        var (theirs, theirsSubject) = commit.Success
            ? (commit.Groups[1].Value, commit.Groups[2].Value)
            : (theirsLabel, sides.TheirsSubject);
        var theirsTitle = Text.Cyan(theirs).Add(Subject(theirs, theirsSubject));

        return new ConflictNames(ours, oursTitle, theirs, theirsTitle);
    }

    // The subject of a side named by a commit alone, none for one named by a branch
    static Text Subject(string name, string subject) =>
        subject != "" && IsCommit(name) ? Text.Dark($" · {subject}") : Text.Empty;

    static bool IsCommit(string name) => name == "HEAD" || Regex.IsMatch(name, @"^(parent of )?[0-9a-f]{7,40}$");
}
