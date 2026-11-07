using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SevenKeyboard;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

internal static unsafe partial class Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct WindowClass
    {
        internal uint Size;
        internal uint Style;
        internal delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nint> Procedure;
        internal int ClassExtra;
        internal int WindowExtra;
        internal nint Instance;
        internal nint Icon;
        internal nint Cursor;
        internal nint Background;
        internal char* MenuName;
        internal char* ClassName;
        internal nint SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Message
    {
        internal nint Hwnd;
        internal uint Id;
        internal nuint WParam;
        internal nint LParam;
        internal uint Time;
        internal int X;
        internal int Y;
        internal uint Private;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint GetModuleHandle(string? name);

    [LibraryImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true)]
    internal static partial ushort RegisterClassEx(in WindowClass windowClass);

    [LibraryImport("user32.dll", EntryPoint = "UnregisterClassW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    internal static partial int UnregisterClass(string className, nint instance);

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    internal static partial nint CreateWindowEx(uint extendedStyle, string className, string title,
        uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial int DestroyWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    internal static partial int ShowWindow(nint hwnd, int command);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    internal static partial nint DefWindowProc(nint hwnd, uint message, nuint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "PeekMessageW")]
    internal static partial int PeekMessage(out Message message, nint hwnd, uint min, uint max, uint remove);

    [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
    internal static partial nint DispatchMessage(in Message message);

    [LibraryImport("user32.dll")]
    internal static partial nint GetShellWindow();

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint RegisterWindowMessage(string name);

    [LibraryImport("user32.dll")]
    internal static partial int DeregisterShellHookWindow(nint hwnd);
}

internal sealed unsafe class WindowClass : IDisposable
{
    internal static readonly string Name = $"ShellHookWindow.Smoke.{Environment.ProcessId}";
    internal static readonly nint Instance = Native.GetModuleHandle(null);
    internal static readonly uint ShellMessage = ShellHookWindow.MessageId;
    internal static readonly List<(nint Receiver, nuint Code, nint Target)> Events = new();

    internal WindowClass()
    {
        fixed (char* name = Name)
        {
            var windowClass = new Native.WindowClass
            {
                Size = (uint)sizeof(Native.WindowClass),
                Procedure = &WindowProcedure,
                Instance = Instance,
                ClassName = name
            };
            if (Native.RegisterClassEx(in windowClass) == 0)
                throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint WindowProcedure(nint hwnd, uint message, nuint wParam, nint lParam)
    {
        if (ShellMessage != 0 && message == ShellMessage)
        {
            Events.Add((hwnd, wParam, lParam));
            return 0;
        }
        return Native.DefWindowProc(hwnd, message, wParam, lParam);
    }

    public void Dispose()
    {
        if (Native.UnregisterClass(Name, Instance) == 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError());
    }
}

internal sealed class TestWindow : IDisposable
{
    internal nint Hwnd { get; private set; }

    internal TestWindow()
    {
        // The receiver stays hidden. Only lifecycle probes are shown, off-screen and without activation.
        Hwnd = Native.CreateWindowEx(0x08040000, WindowClass.Name, "ShellHookWindow smoke",
            0x00CF0000, -32000, -32000, 160, 100, 0, 0, WindowClass.Instance, 0);
        if (Hwnd == 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError());
    }

    internal void Show() => _ = Native.ShowWindow(Hwnd, 4); // SW_SHOWNOACTIVATE

    internal void Destroy()
    {
        if (Hwnd == 0)
            return;
        if (Native.DestroyWindow(Hwnd) == 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        Hwnd = 0;
    }

    public void Dispose()
    {
        if (ShellHookWindow.IsRegistered(Hwnd) && !ShellHookWindow.Deregister(Hwnd))
            throw new InvalidOperationException("Failed to deregister an owned test window.");
        Destroy();
    }
}
