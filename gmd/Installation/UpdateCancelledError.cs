using System.Runtime.CompilerServices;

namespace gmd.Installation;

// An update the user cancelled, which stops it before the new version is put in place, so gmd is
// left as it was. An error to the update, but not one to show as a failure.
class UpdateCancelledError : Error
{
    public UpdateCancelledError(
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
        : base("The update was cancelled", memberName, sourceFilePath, sourceLineNumber) { }
}
