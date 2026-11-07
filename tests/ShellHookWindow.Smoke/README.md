# Smoke checks
Run on Windows with the .NET 10 SDK. No test packages are required.

```powershell
dotnet run --project tests/ShellHookWindow.Smoke -c Release --artifacts-path .artifacts/managed
dotnet publish tests/ShellHookWindow.Smoke -c Release -r win-x64 -p:PublishAot=true --artifacts-path .artifacts/x64
./.artifacts/x64/publish/ShellHookWindow.Smoke/release_win-x64/ShellHookWindow.Smoke.exe --require-shell
```

Repeat publishing/running with `win-x86` and `.artifacts/x86` to check pointer width
and x86 interop. Keep architectures in separate artifacts paths.
NativeAOT publishing needs the Visual C++ toolchain and Windows SDK.

The harness owns a hidden receiving window and pumps its native messages. It creates
and destroys its own off-screen, non-activating top-level windows to obtain actual
`HSHELL_WINDOWCREATED` and `HSHELL_WINDOWDESTROYED` messages. If neither arrives,
that check reports SKIP; use `--require-shell` to make unavailable delivery a failure.
Partial delivery always fails. When delivery works, it also checks silence after
deregistration and delivery after re-registration. No synthetic shell messages are
used, and no other application's windows are touched.

Other checks cover zero/invalid handles, independent registrations, duplicates,
64-bit key identity, repeated/concurrent calls, local state after external changes,
native failure retaining state, and cleanup through a real child-process exit.
The child exits normally, leaving its own remaining windows for OS teardown.
