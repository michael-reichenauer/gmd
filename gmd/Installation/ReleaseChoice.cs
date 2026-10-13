using System.Runtime.InteropServices;
using gmd.Common;

namespace gmd.Installation;

// Which release to update to, and which of its assets is the binary that runs here. Takes the
// releases and the platform rather than reading them, so the tests can drive every platform.
static class ReleaseChoice
{
    // The latest preview, when previews are allowed and it is newer than the latest stable
    // release, otherwise the latest stable release
    public static Release Select(Releases releases, bool allowPreview)
    {
        if (
            allowPreview
            && releases.PreRelease.Assets.Any()
            && IsLeftNewer(releases.PreRelease.Version, releases.StableRelease.Version)
        )
        { // user allow preview versions, and the preview version is newer
            return releases.PreRelease;
        }
        return releases.StableRelease;
    }

    // The names of the release assets that run on a platform, the first a release has being the one
    // to install. An unknown platform has none. 'gmd_linux' is what releases before the arm64 build
    // have, and later ones keep it as a copy of 'gmd_linux_x64' for the updaters that look for it.
    public static IReadOnlyList<string> BinaryNames(OSPlatform os, Architecture arch)
    {
        if (os == OSPlatform.OSX)
            return ["gmd_osx_arm64"];
        if (os == OSPlatform.Linux)
        {
            return arch switch
            {
                Architecture.Arm64 => ["gmd_linux_arm64", "gmd_linux"],
                Architecture.X64 => ["gmd_linux_x64", "gmd_linux"],
                _ => ["gmd_linux"],
            };
        }
        if (os == OSPlatform.Windows)
            return ["gmd_windows"];
        return [];
    }

    // The binary of the release that runs here, null when it has none
    public static Asset? Binary(Release release, IReadOnlyList<string> names) =>
        names.Select(n => release.Assets.FirstOrDefault(a => a.Name == n)).FirstOrDefault(a => a != null);

    static bool IsLeftNewer(string v1Text, string v2Text)
    {
        if (!Version.TryParse(v1Text, out var v1))
        {
            return false;
        }
        if (!Version.TryParse(v2Text, out var v2))
        {
            return true;
        }
        return v1 > v2;
    }
}
