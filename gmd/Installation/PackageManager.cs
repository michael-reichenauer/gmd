namespace gmd.Installation;

// A package manager that gmd was installed with, which then updates it too, rather than gmd itself:
// gmd replacing the binary in the package manager's keeping would leave the package manager taking
// the old version for the one installed. It installs stable releases only, never a preview. Some
// can update gmd only once it is closed, as on Windows, where a program that runs cannot be replaced.
record PackageManager(string Name, string UpdateCommand, bool IsClosedFirst = false)
{
    // 'brew upgrade' alone looks for new versions only once a day, so a release of today would not
    // be found yet
    public static readonly PackageManager Homebrew = new("Homebrew", "brew update && brew upgrade gmd");

    // The package of gmd in the winget repository, microsoft/winget-pkgs. Left out (2026-10-15): its
    // first version was blocked by Defender in winget's validation, as an unsigned program few have
    // run, and gmd is not signed. Known still, for a winget package taken up again.
    public const string WingetId = "MichaelReichenauer.gmd";

    public static readonly PackageManager Winget = new("winget", $"winget upgrade --id {WingetId}", true);

    // The one that installed the binary at the path, the binary itself rather than a link to it, null
    // when none did.
    //
    // Homebrew keeps each version of a formula in a folder of its own, '<prefix>/Cellar/gmd/<version>',
    // with the binary in its 'bin', which it links from '<prefix>/bin', the folder on the PATH. The
    // prefix is /opt/homebrew on macOS and /home/linuxbrew/.linuxbrew on Linux, unless the user chose
    // another, and the formula is named gmd in the tap (michael-reichenauer/homebrew-tap).
    //
    // winget puts a program that is no installer, as gmd.exe, in a folder of its package's,
    // '...\WinGet\Packages\<id>_<source>', in the user's AppData\Local\Microsoft, or in Program
    // Files when installed for all users, and links it from a folder on the PATH, WinGet\Links.
    public static PackageManager? Of(string? binaryPath)
    {
        var parts = (binaryPath ?? "").Split('/', '\\');
        var n = parts.Length;
        if (n >= 5 && parts[n - 2] == "bin" && parts[n - 4] == "gmd" && parts[n - 5] == "Cellar")
            return Homebrew;

        var ignoreCase = StringComparison.OrdinalIgnoreCase;
        if (
            n >= 4
            && parts[n - 4].Equals("WinGet", ignoreCase)
            && parts[n - 3].Equals("Packages", ignoreCase)
            && parts[n - 2].StartsWith($"{WingetId}_", ignoreCase)
        )
            return Winget;

        return null;
    }
}
