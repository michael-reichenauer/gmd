using System.ComponentModel;
using System.Runtime.InteropServices;

namespace gmd.Utils;

// The Windows clipboard, set through the Win32 API.
//
// Windows has no clipboard tool to pipe text into the way the other platforms do. clip.exe comes
// closest, but it decodes its input with the console code page, so anything outside ASCII depends
// on which code page that happens to be, while the API takes the text as UTF-16, which is what
// Windows stores.
//
// The part that matters for the reported "it works, and then it does not, on the same machine":
// only one process at a time may have the clipboard open, so OpenClipboard fails outright while
// another program holds it — a clipboard manager, a password manager or a remote desktop client
// all do so for a moment now and then. A single attempt is therefore a copy that fails at random,
// which is what the retries below are for.
static class WindowsClipboard
{
    const uint CfUnicodeText = 13; // CF_UNICODETEXT
    const uint GmemMoveable = 0x0002; // GMEM_MOVEABLE
    const int OpenAttempts = 10;
    const int OpenDelayMs = 50;

    public static R TrySetText(string text)
    {
        if (!Build.IsWindows)
            return R.Error("The Win32 clipboard is only available on Windows");

        if (!Try(out var e, () => SetText(text)))
            return e;

        return R.Ok;
    }

    static void SetText(string text)
    {
        OpenWithRetries();
        try
        {
            if (!EmptyClipboard())
                throw LastError("EmptyClipboard");

            var handle = AllocateText(text);
            if (SetClipboardData(CfUnicodeText, handle) == IntPtr.Zero)
            {
                // Ownership only passes to the system when the call succeeds
                GlobalFree(handle);
                throw LastError("SetClipboardData");
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    // The text as a moveable global memory block of null terminated UTF-16, which is what
    // CF_UNICODETEXT is. The handle is not freed on success: the clipboard owns it afterwards.
    static IntPtr AllocateText(string text)
    {
        var handle = GlobalAlloc(GmemMoveable, (nuint)((text.Length + 1) * sizeof(char)));
        if (handle == IntPtr.Zero)
            throw LastError("GlobalAlloc");

        var target = GlobalLock(handle);
        if (target == IntPtr.Zero)
        {
            GlobalFree(handle);
            throw LastError("GlobalLock");
        }

        try
        {
            Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
            Marshal.WriteInt16(target, text.Length * sizeof(char), 0);
        }
        finally
        {
            GlobalUnlock(handle);
        }

        return handle;
    }

    static void OpenWithRetries()
    {
        for (int i = 0; i < OpenAttempts; i++)
        {
            if (OpenClipboard(IntPtr.Zero))
                return;

            Thread.Sleep(OpenDelayMs);
        }

        throw LastError($"OpenClipboard after {OpenAttempts} attempts");
    }

    static Exception LastError(string call) => new Win32Exception(Marshal.GetLastWin32Error(), $"{call} failed");

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool OpenClipboard(IntPtr newOwner);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetClipboardData(uint format, IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GlobalAlloc(uint flags, nuint bytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GlobalUnlock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GlobalFree(IntPtr memory);
}
