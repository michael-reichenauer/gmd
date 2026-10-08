using gmd.Cui.Common;
using gmd.Server;

namespace gmd.Cui;

// A tag of the tags dialog: the tag, the commit it is on when the log has it, and whether there is
// an origin to push it to
record TagItem(RepoTag Tag, Commit? Commit, bool HasOrigin)
{
    public bool CanPush => HasOrigin && !Tag.IsOnOrigin;
}

// The rows of the tags dialog, kept apart from the dialog so they can be asserted without a driver.
// One row per tag, the newest commit first, and whether origin has it:
//
//     Tag                 Commit  Date        Origin      Subject
//     v1.1                4dd1e9  2024-10-15  not pushed  Add zeta
//     v1.0                17d85b  2024-10-15  pushed      Add delta
static class TagRows
{
    const int TagWidth = 20;
    const int CommitWidth = 8;
    const int DateWidth = 12;
    const int OriginWidth = 12;
    const int FixedWidth = TagWidth + CommitWidth + DateWidth + OriginWidth;

    // Narrower than this and the subject column has no room to say anything
    public const int MinWidth = FixedWidth + 20;

    // The tags in the order of their commits in the log, newest first, and the tags on commits the
    // log does not have, e.g. beyond its cap, last
    public static IReadOnlyList<TagItem> Items(IReadOnlyList<RepoTag> tags, Repo repo, bool hasOrigin) =>
        tags.Select(t => new TagItem(t, repo.CommitById.TryGetValue(t.CommitId, out var c) ? c : null, hasOrigin))
            .OrderBy(i => i.Commit?.GitIndex ?? int.MaxValue)
            .ThenBy(i => i.Tag.Name, StringComparer.Ordinal)
            .ToList();

    public static Text Header(int width) =>
        Text.Dark("Tag".Max(TagWidth, true))
            .Dark("Commit".Max(CommitWidth, true))
            .Dark("Date".Max(DateWidth, true))
            .Dark("Origin".Max(OriginWidth, true))
            .Dark("Subject".Max(Math.Max(0, width - FixedWidth), true))
            .ToText();

    public static Text Row(TagItem item, int width)
    {
        var name = item.Tag.Name.Max(TagWidth - 1, true) + " ";
        var sid = item.Tag.CommitId.Sid().Max(CommitWidth, true);
        var date = (item.Commit?.AuthorTime.IsoDate() ?? "").Max(DateWidth, true);
        var origin = Origin(item).Max(OriginWidth, true);
        var subject = (item.Commit?.Subject ?? "not in the log").Max(Math.Max(0, width - FixedWidth), true);

        var text = Text.Green(name).Dark(sid).Dark(date);
        text = item.CanPush ? text.Yellow(origin) : text.Dark(origin);
        return text.White(subject).ToText();
    }

    // What the selected tag is, and what the actions would do with it, which the row cannot show
    public static string About(TagItem item)
    {
        var tag =
            item.Commit != null
                ? $"{item.Tag.Name} on {item.Tag.CommitId.Sid()} of '{item.Commit.BranchNiceUniqueName}'"
                : item.Tag.Name;
        if (item.CanPush)
            return $"{tag}, not on origin: Push pushes it";
        if (item.Tag.IsOnOrigin)
            return $"{tag}, on origin too: Remove removes it there as well";
        return tag;
    }

    // What the status line says once a tag is pushed or removed
    public static string Pushed(string name) => $"Pushed the tag '{name}' to origin";

    public static string Removed(RepoTag tag) =>
        tag.IsOnOrigin ? $"Removed the tag '{tag.Name}' here and on origin" : $"Removed the tag '{tag.Name}'";

    static string Origin(TagItem item) =>
        item.Tag.IsOnOrigin ? "pushed"
        : item.HasOrigin ? "not pushed"
        : "";
}
