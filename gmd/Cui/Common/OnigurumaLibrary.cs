using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace gmd.Cui.Common;

// Where Oniguruma (libonigwrap), the native library that runs the regular expressions of the grammars
// code is colored by (CodeGrammars), is loaded from. A build has it beside gmd, where it is found as
// any library is; a release, a single file, has it embedded for its platform (gmd.csproj), and it is
// written from there into a folder of the user's own, named by its hash, and loaded from that file.
// Whatever fails, nothing is loaded, and code is drawn in the text's color.
//
// Not the .NET host's own extraction of a single file's native libraries, which it does as gmd starts:
// where it cannot write its folder, gmd does not start at all, nor does the updater's check of a new
// version (BinaryInstaller), which would have refused every update since.
static class OnigurumaLibrary
{
    const string Name = "libonigwrap";
    const string ResourceName = "gmd.native.libonigwrap";

    static int isRegistered;
    static readonly Lazy<IntPtr> Handle = new(Load);

    // Lets Onigwrap, the managed side of Oniguruma, load the library as said above, once
    public static void Register()
    {
        if (Interlocked.Exchange(ref isRegistered, 1) == 1)
            return;

        if (
            Result.Catch(() => NativeLibrary.SetDllImportResolver(typeof(Onigwrap.OnigRegExp).Assembly, Resolve))
            is Error e
        )
            Log.Warn($"Oniguruma is loaded as any library is: {e.AllMessages()}");
    }

    static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != Name)
            return IntPtr.Zero;
        if (NativeLibrary.TryLoad(name, assembly, searchPath, out var handle))
            return handle;
        return Handle.Value;
    }

    // The embedded library, written out and loaded, or zero, which leaves code uncolored
    static IntPtr Load()
    {
        using var stream = typeof(OnigurumaLibrary).Assembly.GetManifestResourceStream(ResourceName);
        if (stream == null)
        {
            Log.Warn("Oniguruma is neither beside gmd nor in it");
            return IntPtr.Zero;
        }
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);

        // Not verified, since a folder that is not there yet is no path at all, which was the current one
        var data = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify
        );
        if (!Path.IsPathRooted(data))
        {
            Log.Warn($"No folder to write Oniguruma to: '{data}'");
            return IntPtr.Zero;
        }
        if (Write(bytes, Path.Join(data, "gmd"), FileName) is not string path)
            return IntPtr.Zero;
        if (!NativeLibrary.TryLoad(path, out var handle))
        {
            Log.Warn($"Failed to load Oniguruma from '{path}'");
            return IntPtr.Zero;
        }
        Log.Info($"Loaded Oniguruma from '{path}'");
        return handle;
    }

    static string FileName =>
        OperatingSystem.IsWindows() ? $"{Name}.dll"
        : OperatingSystem.IsMacOS() ? $"{Name}.dylib"
        : $"{Name}.so";

    // The path of a file of the bytes in the folder, named by their hash before the name, written unless
    // it is there already as it should be, or null where it could not be written. The folder is made
    // the user's alone, since a library put there by someone else would be run as the user. The file
    // is written beside and renamed into place, so that a gmd starting at the same time never loads
    // half a file; one that is loaded by another gmd, which Windows does not let be replaced, is used as
    // it is, if it is the same.
    internal static string? Write(byte[] bytes, string folder, string name)
    {
        var hash = Convert.ToHexString(SHA256.HashData(bytes))[..16].ToLowerInvariant();
        var path = Path.Join(folder, $"{hash}-{name}");
        if (IsWritten(path, bytes))
            return path;

        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        var result = Result.Catch(() =>
        {
            if (OperatingSystem.IsWindows())
                Directory.CreateDirectory(folder);
            else
                Directory.CreateDirectory(
                    folder,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                );
            File.WriteAllBytes(temp, bytes);
            File.Move(temp, path, overwrite: true);
        });
        if (result is Error e)
        {
            Result.Catch(() => File.Delete(temp));
            if (IsWritten(path, bytes))
                return path;
            Log.Warn($"Failed to write Oniguruma to '{path}': {e.AllMessages()}");
            return null;
        }
        return path;
    }

    static bool IsWritten(string path, byte[] bytes) =>
        Result.Catch(() => File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) is true;
}
