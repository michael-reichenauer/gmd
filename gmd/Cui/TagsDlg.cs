using gmd.Cui.Common;

namespace gmd.Cui;

enum TagAction
{
    Show,
    Push,
    Remove,
}

// What the user picked in the tags dialog: an action, and the tag it is for
record TagChoice(TagAction Action, TagItem Item);

interface ITagsDlg
{
    Result<TagChoice> Show(IReadOnlyList<TagItem> tags);
}

// Every tag of the repository, one row each, see TagRows, in a ListDlg: it returns what was picked
// and closes, and the command acts on it. Show moves the log to the tag's commit, Push pushes a tag
// origin does not have, and Remove asks, then removes it, on origin too when it is there.
class TagsDlg : ITagsDlg
{
    const int maxWidth = 110;

    public Result<TagChoice> Show(IReadOnlyList<TagItem> tags) =>
        new ListDlg<TagItem, TagChoice>("Tags", TagRows.MinWidth, maxWidth, TagRows.Header, TagRows.Row, TagRows.About)
            .Action("_Show", t => new TagChoice(TagAction.Show, t), t => t.Commit != null)
            .Action("_Push", t => new TagChoice(TagAction.Push, t), t => t.CanPush)
            .Action("_Remove", t => new TagChoice(TagAction.Remove, t))
            .Show(tags);
}
