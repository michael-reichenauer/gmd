using System.Diagnostics;
using System.Text;
using TerminalClipboard = Terminal.Gui.Clipboard;

namespace gmd.Utils;

static class Clipboard
{
    public static R Set(string text)
    {
        // if (Build.IsMacOs) // Does it work ????
        // {
        //    return OsxClipboard.SetText(text);
        // }
        // else
        if (Build.IsLinux)
        {
            return LinuxClipboard.TrySetText(text);
        }
        else
        if (Build.IsWindows)
        {
            return WindowsClipboard.TrySetText(text);
        }

        return R.Error("Clipboard not supported on this platform");
    }
}


// https://github.com/CopyText/TextCopy/blob/main/src/TextCopy/LinuxClipboard_2.1.cs
static class LinuxClipboard
{
    static bool isWsl;
    static Cmd cmd = new Cmd();

    static LinuxClipboard()
    {
        isWsl = Environment.GetEnvironmentVariable("WSL_DISTRO_NAME") != null;
    }


    public static R TrySetText(string text)
    {
        var tempFileName = Path.GetTempFileName();
        File.WriteAllText(tempFileName, text);
        return InnerSetText(tempFileName);
    }

    static R InnerSetText(string tempFileName)
    {
        try
        {
            if (isWsl)
            {
                return Cmd.Run($"bash -c \"cat {tempFileName} | clip.exe \"");
            }
            else
            {
                return Cmd.Run($"bash -c \"cat {tempFileName} | xsel -i --clipboard \"");
            }
        }
        finally
        {
            if (File.Exists(tempFileName)) File.Delete(tempFileName);
        }
    }

    public static R<string> GetText()
    {
        var tempFileName = Path.GetTempFileName();
        try
        {
            if (!Try(out var e, InnerGetText(tempFileName))) return e;

            if (!Try(out string? text, out e, File.ReadAllText(tempFileName))) return e;

            return text;
        }
        finally
        {
            if (File.Exists(tempFileName)) File.Delete(tempFileName);
        }
    }

    static R InnerGetText(string tempFileName)
    {
        if (isWsl)
        {
            return Cmd.Run($"powershell.exe -NoProfile Get-Clipboard  > {tempFileName}");
        }
        else
        {
            return Cmd.Run($"xsel -o --clipboard  > {tempFileName}");
        }
    }
}


static class BashRunner
{
    public static string Run(string commandLine)
    {
        StringBuilder errorBuilder = new();
        StringBuilder outputBuilder = new();
        var arguments = $"-c \"{commandLine}\"";
        using Process process = new()
        {
            StartInfo = new()
            {
                FileName = "bash",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = false,
            }
        };
        process.Start();
        process.OutputDataReceived += (_, args) => { outputBuilder.AppendLine(args.Data); };
        process.BeginOutputReadLine();
        process.ErrorDataReceived += (_, args) => { errorBuilder.AppendLine(args.Data); };
        process.BeginErrorReadLine();
        process.WaitForExit();
        // if (!process.DoubleWaitForExit())
        // {
        //     var timeoutError = $"Error: bash {arguments}\nProcess timeout: {errorBuilder}\nOutput: {outputBuilder}";
        //     throw new(timeoutError);
        // }
        if (process.ExitCode == 0)
        {
            return outputBuilder.ToString();
        }

        var error = $"Error: bash {arguments}\n{errorBuilder}\nOutput: {outputBuilder}";
        throw new(error);
    }

    // //To work around https://github.com/dotnet/runtime/issues/27128
    // static bool DoubleWaitForExit(this Process process)
    // {
    //     var result = process.WaitForExit(1500);
    //     if (result)
    //     {
    //         process.WaitForExit();
    //     }
    //     return result;
    // }
}

static class WindowsClipboard
{
    public static R TrySetText(string text)
    {
        if (!TerminalClipboard.TrySetClipboardData(text)) return R.Error("Failed to set clipboard");
        return R.Ok;
    }
}



// using System.ComponentModel;
// using System.Runtime.InteropServices;

// static class WindowsClipboard
// {
//     public static async Task SetTextAsync(string text, Cancellation cancellation)
//     {
//         await TryOpenClipboardAsync(cancellation);

//         InnerSet(text);
//     }

//     public static void SetText(string text)
//     {
//         TryOpenClipboard();

//         InnerSet(text);
//     }

//     static void InnerSet(string text)
//     {
//         EmptyClipboard();
//         IntPtr hGlobal = default;
//         try
//         {
//             var bytes = (text.Length + 1) * 2;
//             hGlobal = Marshal.AllocHGlobal(bytes);

//             if (hGlobal == default)
//             {
//                 ThrowWin32();
//             }

//             var target = GlobalLock(hGlobal);

//             if (target == default)
//             {
//                 ThrowWin32();
//             }

//             try
//             {
//                 Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
//             }
//             finally
//             {
//                 GlobalUnlock(target);
//             }

//             if (SetClipboardData(cfUnicodeText, hGlobal) == default)
//             {
//                 ThrowWin32();
//             }

//             hGlobal = default;
//         }
//         finally
//         {
//             if (hGlobal != default)
//             {
//                 Marshal.FreeHGlobal(hGlobal);
//             }

//             CloseClipboard();
//         }
//     }

//     static async Task TryOpenClipboardAsync(Cancellation cancellation)
//     {
//         var num = 10;
//         while (true)
//         {
//             if (OpenClipboard(default))
//             {
//                 break;
//             }

//             if (--num == 0)
//             {
//                 ThrowWin32();
//             }

//             await Task.Delay(100, cancellation);
//         }
//     }

//     static void TryOpenClipboard()
//     {
//         var num = 10;
//         while (true)
//         {
//             if (OpenClipboard(default))
//             {
//                 break;
//             }

//             if (--num == 0)
//             {
//                 ThrowWin32();
//             }

//             Thread.Sleep(100);
//         }
//     }

//     public static async Task<string?> GetTextAsync(Cancellation cancellation)
//     {
//         if (!IsClipboardFormatAvailable(cfUnicodeText))
//         {
//             return null;
//         }
//         await TryOpenClipboardAsync(cancellation);

//         return InnerGet();
//     }

//     public static string? GetText()
//     {
//         if (!IsClipboardFormatAvailable(cfUnicodeText))
//         {
//             return null;
//         }
//         TryOpenClipboard();

//         return InnerGet();
//     }

//     static string? InnerGet()
//     {
//         IntPtr handle = default;

//         IntPtr pointer = default;
//         try
//         {
//             handle = GetClipboardData(cfUnicodeText);
//             if (handle == default)
//             {
//                 return null;
//             }

//             pointer = GlobalLock(handle);
//             if (pointer == default)
//             {
//                 return null;
//             }

//             var size = GlobalSize(handle);
//             var buff = new byte[size];

//             Marshal.Copy(pointer, buff, 0, size);

//             return Encoding.Unicode.GetString(buff).TrimEnd('\0');
//         }
//         finally
//         {
//             if (pointer != default)
//             {
//                 GlobalUnlock(handle);
//             }

//             CloseClipboard();
//         }
//     }

//     const uint cfUnicodeText = 13;

//     static void ThrowWin32()
//     {
//         throw new Win32Exception(Marshal.GetLastWin32Error());
//     }

//     [DllImport("User32.dll", SetLastError = true)]
//     [return: MarshalAs(UnmanagedType.Bool)]
//     static extern bool IsClipboardFormatAvailable(uint format);

//     [DllImport("User32.dll", SetLastError = true)]
//     static extern IntPtr GetClipboardData(uint uFormat);

//     [DllImport("kernel32.dll", SetLastError = true)]
//     static extern IntPtr GlobalLock(IntPtr hMem);

//     [DllImport("kernel32.dll", SetLastError = true)]
//     [return: MarshalAs(UnmanagedType.Bool)]
//     static extern bool GlobalUnlock(IntPtr hMem);

//     [DllImport("user32.dll", SetLastError = true)]
//     [return: MarshalAs(UnmanagedType.Bool)]
//     static extern bool OpenClipboard(IntPtr hWndNewOwner);

//     [DllImport("user32.dll", SetLastError = true)]
//     [return: MarshalAs(UnmanagedType.Bool)]
//     static extern bool CloseClipboard();

//     [DllImport("user32.dll", SetLastError = true)]
//     static extern IntPtr SetClipboardData(uint uFormat, IntPtr data);

//     [DllImport("user32.dll")]
//     static extern bool EmptyClipboard();

//     [DllImport("Kernel32.dll", SetLastError = true)]
//     static extern int GlobalSize(IntPtr hMem);
// }

// #if (NETSTANDARD || NETFRAMEWORK || NET5_0_OR_GREATER)
// using System.Runtime.InteropServices;

// static class OsxClipboard
// {
//     static IntPtr nsString = objc_getClass("NSString");
//     static IntPtr nsPasteboard = objc_getClass("NSPasteboard");
//     static IntPtr nsStringPboardType;
//     static IntPtr utfTextType;
//     static IntPtr generalPasteboard;
//     static IntPtr initWithUtf8Register = sel_registerName("initWithUTF8String:");
//     static IntPtr allocRegister = sel_registerName("alloc");
//     static IntPtr setStringRegister = sel_registerName("setString:forType:");
//     static IntPtr stringForTypeRegister = sel_registerName("stringForType:");
//     static IntPtr utf8Register = sel_registerName("UTF8String");
//     static IntPtr generalPasteboardRegister = sel_registerName("generalPasteboard");
//     static IntPtr clearContentsRegister = sel_registerName("clearContents");

//     static OsxClipboard()
//     {
//         utfTextType = objc_msgSend(objc_msgSend(nsString, allocRegister), initWithUtf8Register, "public.utf8-plain-text");
//         nsStringPboardType = objc_msgSend(objc_msgSend(nsString, allocRegister), initWithUtf8Register, "NSStringPboardType");

//         generalPasteboard = objc_msgSend(nsPasteboard, generalPasteboardRegister);
//     }

//     public static string? GetText()
//     {
//         var ptr = objc_msgSend(generalPasteboard, stringForTypeRegister, nsStringPboardType);
//         var charArray = objc_msgSend(ptr, utf8Register);
//         return Marshal.PtrToStringAnsi(charArray);
//     }

//     public static Task<string?> GetTextAsync(Cancellation cancellation)
//     {
//         return Task.FromResult(GetText());
//     }

//     public static void SetText(string text)
//     {
//         IntPtr str = default;
//         try
//         {
//             str = objc_msgSend(objc_msgSend(nsString, allocRegister), initWithUtf8Register, text);
//             objc_msgSend(generalPasteboard, clearContentsRegister);
//             objc_msgSend(generalPasteboard, setStringRegister, str, utfTextType);
//         }
//         finally
//         {
//             if (str != default)
//             {
//                 objc_msgSend(str, sel_registerName("release"));
//             }
//         }
//     }

//     public static Task SetTextAsync(string text, Cancellation cancellation)
//     {
//         SetText(text);
//         return Task.CompletedTask;
//     }

//     [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
//     static extern IntPtr objc_getClass(string className);

//     [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
//     static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

//     [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
//     static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, string arg1);

//     [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
//     static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1);

//     [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
//     static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

//     [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
//     static extern IntPtr sel_registerName(string selectorName);
// }
// #endif