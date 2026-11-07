# shell-hook-window
Shell hook registration for .NET 10 on Windows, compatible with NativeAOT.

```csharp
using SevenKeyboard;

uint shellMessage = ShellHookWindow.MessageId;
bool registered = ShellHookWindow.Register(hwnd); // Required caller-owned HWND.
// In your window procedure: if (message == shellMessage), handle wParam/lParam.
bool tracked = ShellHookWindow.IsRegistered(hwnd);
bool removed = ShellHookWindow.Deregister(hwnd); // Before destroying the window.
```

The caller provides the window, message loop and message handling. `MessageId` is
cached and is zero if native message registration fails. Native shell message
parameters keep their original meaning and lifetime; see the
[Windows API](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registershellhookwindow).

Registration and deregistration are idempotent, not reference counted. Both return
`false` for zero; native failures also return `false`. Deregistering an untracked
nonzero handle succeeds. `IsRegistered` only reads local tracking, even after
external changes or window destruction. Native deregistration failure retains that
tracking. Calls are serialized; normal process exit attempts cleanup of remaining
handles, but callers should deregister explicitly before destroying their windows.

Version 3.0.0 removes AHK's default HWND and callback/OnMessage APIs. Handles are
stored at native pointer width. See [smoke checks](tests/ShellHookWindow.Smoke/README.md).
