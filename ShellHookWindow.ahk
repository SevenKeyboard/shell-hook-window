#Requires AutoHotkey v1.1.35+
;==============================================================
; ShellHookWindow — Simple shell hook registration helper
;
; GitHub: https://github.com/SevenKeyboard/shell-hook-window
; Author: SevenKeyboard Ltd. (2025)
; License: The Unlicense
;==============================================================
class VersionManager_ShellHookWindow
{
    static _ := VersionManager_ShellHookWindow._init()
    _init()    {
        global
        SHELLHOOKWINDOW_VERSION := "1.0.0"
    }
}
class ShellHookWindow
{
    static msgNumber := 0
    register(callback, maxThreads := 1, str := "SHELLHOOK")    {
        dllCall("User32.dll\RegisterShellHookWindow", "Ptr",A_ScriptHwnd)
        onMessage(this.msgNumber := dllCall("User32.dll\RegisterWindowMessage", "Str",str), callback, maxThreads)
    }
    unregister()     {
        dllCall("User32.dll\DeregisterShellHookWindow", "Ptr",A_ScriptHwnd)
    }
    unregisterOnExit(addRemove := 1)    {
        static objbmUnregister := false
        if (!objbmUnregister)
            objbmUnregister := objBindMethod(this,"unregister")
        onExit(objbmUnregister, addRemove)
    }
}