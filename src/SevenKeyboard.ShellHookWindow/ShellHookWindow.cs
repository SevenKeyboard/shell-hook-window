//==============================================================
// ShellHookWindow — Simple shell hook registration helper
//
// GitHub: https://github.com/SevenKeyboard/shell-hook-window
// Author: SevenKeyboard Ltd. (2026)
// License: The Unlicense
//==============================================================

using System.Runtime.InteropServices;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace SevenKeyboard;

/// <summary>Registers caller-owned windows for native shell notifications.</summary>
public static partial class ShellHookWindow
{
    private static readonly object Gate = new();
    private static readonly HashSet<nint> RegisteredHwnds = new();
    private static bool _exiting;

    /// <summary>The cached RegisterWindowMessageW("SHELLHOOK") result; zero on native failure.</summary>
    /// <remarks>Handle this message in the caller's window procedure. Its parameters retain
    /// their native meaning and lifetime; some lParam values are temporary pointers.</remarks>
    public static uint MessageId => ShellMessage.Id;

    /// <summary>Registers hwnd, or succeeds immediately if it is already tracked.</summary>
    /// <remarks>Zero and native registration failures return false. The caller owns the
    /// window and its message loop. Duplicate registrations are not reference counted.</remarks>
    public static bool Register(nint hwnd)
    {
        if (hwnd == 0)
            return false;

        lock (Gate)
        {
            if (_exiting)
                return false;
            if (RegisteredHwnds.Contains(hwnd))
                return true;
            if (RegisterShellHookWindow(hwnd) == 0)
                return false;

            RegisteredHwnds.Add(hwnd);
            if (RegisteredHwnds.Count == 1)
                AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            return true;
        }
    }

    /// <summary>Unregisters hwnd, or succeeds immediately if a nonzero handle is not tracked.</summary>
    /// <remarks>Zero returns false. Native failure leaves the handle tracked for retry.
    /// Call before destroying the window; handles can be reused by Windows.</remarks>
    public static bool Deregister(nint hwnd)
    {
        if (hwnd == 0)
            return false;

        lock (Gate)
        {
            if (!RegisteredHwnds.Contains(hwnd))
                return true;
            if (DeregisterShellHookWindow(hwnd) == 0)
                return false;

            RegisteredHwnds.Remove(hwnd);
            if (RegisteredHwnds.Count == 0)
                AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            return true;
        }
    }

    /// <summary>Returns local tracking state, without querying Windows or validating hwnd.</summary>
    /// <remarks>Window destruction or registration changes outside this helper do not update it.</remarks>
    public static bool IsRegistered(nint hwnd)
    {
        lock (Gate)
            return RegisteredHwnds.Contains(hwnd);
    }

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        lock (Gate)
        {
            _exiting = true;
            foreach (nint hwnd in RegisteredHwnds)
            {
                try
                {
                    _ = DeregisterShellHookWindow(hwnd);
                }
                catch (Exception)
                {
                    // Exit cleanup is best effort; still attempt every remaining handle.
                }
            }
            RegisteredHwnds.Clear();
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        }
    }

    private static class ShellMessage
    {
        internal static readonly uint Id = RegisterWindowMessage("SHELLHOOK");
    }

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterWindowMessage(string message);

    [LibraryImport("user32.dll")]
    private static partial int RegisterShellHookWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    private static partial int DeregisterShellHookWindow(nint hwnd);
}
