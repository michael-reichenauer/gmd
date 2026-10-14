namespace gmd.Installation;

// A package manager that gmd was installed with, which then updates it too, rather than gmd itself:
// gmd replacing the binary in the package manager's keeping would leave the package manager taking
// the old version for the one installed. It installs stable releases only, never a preview.
record PackageManager(string Name, string UpdateCommand)
{
    // 'brew upgrade' alone looks for new versions only once a day, so a release of today would not
    // be found yet
    public static readonly PackageManager Homebrew = new("Homebrew", "brew update && brew upgrade gmd");

    // The one that installed the binary at the path, the binary itself rather than a link to it, null
    // when none did.
    //
    // Homebrew keeps each version of a formula in a folder of its own, '<prefix>/Cellar/gmd/<version>',
    // with the binary in its 'bin', which it links from '<prefix>/bin', the folder on the PATH. The
    // prefix is /opt/homebrew on macOS and /home/linuxbrew/.linuxbrew on Linux, unless the user chose
    // another, and the formula is named gmd in the tap (michael-reichenauer/homebrew-tap).
    public static PackageManager? Of(string? binaryPath)
    {
        var parts = (binaryPath ?? "").Split('/');
        var n = parts.Length;
        if (n >= 5 && parts[n - 2] == "bin" && parts[n - 4] == "gmd" && parts[n - 5] == "Cellar")
            return Homebrew;

        return null;
    }
}
