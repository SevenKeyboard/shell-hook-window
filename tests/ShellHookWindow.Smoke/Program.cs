using System.Diagnostics;
using SevenKeyboard;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Contains("--exit-probe"))
                return RunExitProbe();
            Run(args.Contains("--require-shell"));
            Console.WriteLine($"PASS: ShellHookWindow smoke ({System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}).");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void Run(bool requireShell)
    {
        uint message = ShellHookWindow.MessageId;
        Check(message >= 0xC000 && message <= 0xFFFF, "Registered message range");
        Check(message == Native.RegisterWindowMessage("SHELLHOOK"), "Native message ID agreement");
        Check(message == ShellHookWindow.MessageId, "Cached message ID");
        Check(!ShellHookWindow.Register(0), "Zero registration");
        Check(!ShellHookWindow.Deregister(0), "Zero deregistration");
        Check(!ShellHookWindow.IsRegistered(0), "Zero tracking");
        Check(!ShellHookWindow.Register(-1), "Invalid HWND registration");
        Check(!ShellHookWindow.IsRegistered(-1), "Invalid HWND not tracked");
        Check(ShellHookWindow.Deregister(-1), "Untracked nonzero deregistration");

        using var windowClass = new WindowClass();
        using var receiver = new TestWindow();
        using var other = new TestWindow();
        nint hwnd = receiver.Hwnd;
        Check(!ShellHookWindow.IsRegistered(hwnd), "Initially untracked");
        Check(ShellHookWindow.Deregister(hwnd), "Never-registered window deregistration");
        Check(ShellHookWindow.Register(hwnd), "Registration");
        Check(ShellHookWindow.Register(hwnd), "Duplicate registration");
        Check(ShellHookWindow.IsRegistered(hwnd), "Registration tracking");
        Check(ShellHookWindow.Register(other.Hwnd), "Second window registration");
        Check(ShellHookWindow.Deregister(other.Hwnd), "Independent second window removal");
        Check(ShellHookWindow.IsRegistered(hwnd), "First window still tracked");

        if (nint.Size == 8)
        {
            nint differentHighBits = unchecked((nint)((long)hwnd ^ (1L << 32)));
            Check(!ShellHookWindow.IsRegistered(differentHighBits), "Full-width lookup");
            Check(ShellHookWindow.Deregister(differentHighBits), "Untracked high-bit alias");
            Check(ShellHookWindow.IsRegistered(hwnd), "High bits must not alias a tracked handle");
        }

        bool shellSupported = ProbeLifecycle(hwnd, expected: null);
        if (!shellSupported)
        {
            Check(!requireShell, "Real shell lifecycle notifications required");
            Console.WriteLine($"SKIP: actual shell notifications unavailable (shell HWND present: {Native.GetShellWindow() != 0}).");
        }
        else
        {
            Console.WriteLine("PASS: actual HSHELL_WINDOWCREATED / HSHELL_WINDOWDESTROYED.");
        }

        Check(ShellHookWindow.Deregister(hwnd), "Deregistration");
        Check(!ShellHookWindow.IsRegistered(hwnd), "One deregistration removes duplicate registrations");
        Check(ShellHookWindow.Deregister(hwnd), "Duplicate deregistration");
        if (shellSupported)
            _ = ProbeLifecycle(hwnd, expected: false);
        Check(ShellHookWindow.Register(hwnd), "Re-registration");
        if (shellSupported)
            _ = ProbeLifecycle(hwnd, expected: true);
        Check(ShellHookWindow.Deregister(hwnd), "Re-registration cleanup");

        for (int i = 0; i < 100; i++)
        {
            Check(ShellHookWindow.Register(hwnd), "Repeated registration");
            Check(ShellHookWindow.Deregister(hwnd), "Repeated cleanup");
        }
        Parallel.For(0, 32, _ => Check(ShellHookWindow.Register(hwnd), "Concurrent duplicate registration"));
        Parallel.For(0, 32, _ => Check(ShellHookWindow.Deregister(hwnd), "Concurrent duplicate deregistration"));
        Check(!ShellHookWindow.IsRegistered(hwnd), "Concurrent cleanup");
        Console.WriteLine("PASS: invalid HWND, duplicates, independent windows, full-width keys and repeated/concurrent cleanup.");
        VerifyProcessExit();
    }

    private static bool ProbeLifecycle(nint receiver, bool? expected)
    {
        PumpUntil(() => false, 50);
        WindowClass.Events.Clear();
        using var probe = new TestWindow();
        nint target = probe.Hwnd;
        probe.Show();
        int timeout = expected == false ? 350 : 2000;
        PumpUntil(() => HasEvent(receiver, target, 1), timeout);
        bool created = HasEvent(receiver, target, 1);
        probe.Destroy();
        PumpUntil(() => HasEvent(receiver, target, 2), timeout);
        bool destroyed = HasEvent(receiver, target, 2);
        if (expected is bool required)
        {
            Check(created == required && destroyed == required,
                $"Shell delivery expected={required}, created={created}, destroyed={destroyed}");
        }
        else
        {
            // No delivery can be an isolated desktop without a shell; partial delivery is a failure.
            Check(created == destroyed, $"Partial shell delivery: created={created}, destroyed={destroyed}");
        }
        return created && destroyed;
    }

    private static bool HasEvent(nint receiver, nint target, nuint code) =>
        WindowClass.Events.Any(item => item.Receiver == receiver && item.Target == target && item.Code == code);

    private static void PumpUntil(Func<bool> condition, int milliseconds)
    {
        var timer = Stopwatch.StartNew();
        do
        {
            while (Native.PeekMessage(out Native.Message message, 0, 0, 0, 1) != 0)
                _ = Native.DispatchMessage(in message);
            if (condition())
                return;
            Thread.Sleep(5);
        } while (timer.ElapsedMilliseconds < milliseconds);
    }

    private static void VerifyProcessExit()
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        if (Path.GetFileNameWithoutExtension(start.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "ShellHookWindow.Smoke.dll"));
        start.ArgumentList.Add("--exit-probe");
        using Process child = Process.Start(start) ?? throw new InvalidOperationException("Could not start exit probe.");
        Task<string> output = child.StandardOutput.ReadToEndAsync();
        Task<string> errors = child.StandardError.ReadToEndAsync();
        Check(child.WaitForExit(15000), "Exit probe timed out");
        string text = output.GetAwaiter().GetResult();
        Check(child.ExitCode == 0 && text.Contains("PASS: real ProcessExit cleanup"),
            $"Exit probe failed ({child.ExitCode}): {text} {errors.GetAwaiter().GetResult()}");
        Console.Write(text);
    }

    private static int RunExitProbe()
    {
        // Intentionally retain live windows until process exit; the OS then destroys them.
        // A destroyed registration first exercises failed deregistration and stale local tracking.
        _ = new WindowClass();
        var destroyed = new TestWindow();
        nint stale = destroyed.Hwnd;
        Check(ShellHookWindow.Register(stale), "Stale-window initial registration");
        destroyed.Destroy();
        Check(ShellHookWindow.IsRegistered(stale), "Destroyed window remains locally tracked");
        Check(ShellHookWindow.Register(stale), "Tracked duplicate does not query OS");
        Check(!ShellHookWindow.Deregister(stale), "Destroyed HWND native deregistration failure");
        Check(ShellHookWindow.IsRegistered(stale), "Native failure preserves tracking");

        var first = new TestWindow();
        var second = new TestWindow();
        Check(first.Hwnd != stale && second.Hwnd != stale, "Stale test handle was not reused");
        Check(ShellHookWindow.Register(first.Hwnd), "First exit registration");
        Check(ShellHookWindow.Register(second.Hwnd), "Second exit registration");
        Check(Native.DeregisterShellHookWindow(second.Hwnd) != 0, "Out-of-band deregistration");
        Check(ShellHookWindow.IsRegistered(second.Hwnd), "External native change leaves local tracking");

        // Subscribed after the library's handler, so this observes actual shutdown cleanup.
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            bool cleared = !ShellHookWindow.IsRegistered(stale)
                && !ShellHookWindow.IsRegistered(first.Hwnd)
                && !ShellHookWindow.IsRegistered(second.Hwnd);
            Environment.ExitCode = cleared ? 0 : 1;
            Console.WriteLine(cleared ? "PASS: real ProcessExit cleanup (live, stale and externally deregistered handles)."
                : "FAIL: ProcessExit retained registrations.");
        };
        return 0;
    }

    private static void Check(bool condition, string label)
    {
        if (!condition)
            throw new InvalidOperationException(label);
    }
}
