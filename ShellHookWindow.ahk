#Requires AutoHotkey v2.0.0+
;==============================================================
; ShellHookWindow — Simple shell hook registration helper
;
; GitHub: https://github.com/SevenKeyboard/shell-hook-window
; Author: SevenKeyboard Ltd. (2025)
; License: The Unlicense
;==============================================================
class VersionManager_ShellHookWindow
{
    static _ := this._init()
    static _init()    {
        global
        SHELLHOOKWINDOW_VERSION := "1.0.0"
    }
}
class ShellHookWindow
{
    static msgNumber := 0
    static register(callback, maxThreads := 1, str := "SHELLHOOK")    {
        dllCall("User32.dll\RegisterShellHookWindow", "Ptr",A_ScriptHwnd)
        onMessage(this.msgNumber := dllCall("User32.dll\RegisterWindowMessage", "Str",str), callback, maxThreads)
    }
    static unregister(*)     {
        dllCall("User32.dll\DeregisterShellHookWindow", "Ptr",A_ScriptHwnd)
    }
    static unregisterOnExit(addRemove := 1)    {
        static objbmUnregister := objBindMethod(this,"unregister")
        onExit(objbmUnregister, addRemove)
    }
}