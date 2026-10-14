using gmd.Cui.Common;
using gmd.Git;

namespace gmd.Cui.Common;

// The check of a branch or tag name typed into a dialog, see RefName. The message names no name,
// since a dialog's messages are made with the dialog, before anything is typed: one that did named
// the name the dialog opened with.
static class RefNameValidation
{
    // Only when 'when' says so, if given, e.g. for a new branch rather than one picked from a list
    public static void ValidateRefName(this UIDialog dlg, Func<string> name, string what, Func<bool>? when = null) =>
        dlg.Validate(
            () => when?.Invoke() == false || RefName.IsValid(name()),
            $"Not a {what} name git takes:\nno spaces, none of ~ ^ : ? * [ \\ \", and no '..'"
        );
}
