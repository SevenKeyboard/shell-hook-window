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
        SHELLHOOKWINDOW_VERSION := "2.0.0"
    }
}
class ShellHookWindow
{
    ensureOnMessage(callback, maxThreads := 1)    {
        return this.register(A_ScriptHwnd)
            ? this.onMessage(callback, maxThreads)
            : ""
    }
    ;-------------------------------------------------------
    WM_SHELLHOOKMESSAGE    {
        get  {
            static msgNumber := dllCall("User32.dll\RegisterWindowMessage", "Str","SHELLHOOK", "UInt")
            return msgNumber
        }
    }
    register(hwnd := "UNSET")    {
        if (hwnd == "UNSET")
            hwnd := A_ScriptHwnd
        if (!hwnd)
            return false
        if (this._hasRegisteredHwnd(hwnd))
            return true
        needsOnExit := !this._RegisteredHwndCount
        ok := dllCall("User32.dll\RegisterShellHookWindow", "Ptr",hwnd, "Int")
        if (ok)    {
            this._setRegisteredHwnd(hwnd)
            if (needsOnExit)
                onExit(this._objbmOnExiting, -1)
        }
        return ok
    }
    deregister(hwnd := "UNSET")    {
        if (hwnd == "UNSET")
            hwnd := A_ScriptHwnd
        if (!hwnd)
            return false
        if (!this._hasRegisteredHwnd(hwnd))
            return true
        ok := dllCall("User32.dll\DeregisterShellHookWindow", "Ptr",hwnd, "Int")
        if (ok)    {
            this._deleteRegisteredHwnd(hwnd)
            if (!this._RegisteredHwndCount)
                onExit(this._objbmOnExiting, 0)
        }
        return ok
    }
    isRegistered(hwnd := "UNSET")    {
        if (hwnd == "UNSET")
            hwnd := A_ScriptHwnd
        return this._hasRegisteredHwnd(hwnd)
    }
    onMessage(callback, maxThreads := 1)    {
        return onMessage(this.WM_SHELLHOOKMESSAGE, callback, maxThreads)
    }
    ;-------------------------------------------------------
    static _objbmOnExiting := objBindMethod(ShellHookWindow, "_onExiting")
    _onExiting(_1, _2)    {
        hwnds := this._registeredHwnds.clone(), this._registeredHwnds := object()
        for _,hwnd in hwnds
            dllCall("User32.dll\DeregisterShellHookWindow", "Ptr",hwnd, "Int")
    }
    ;-------------------------------------------------------
    static _registeredHwnds := object()
    _setRegisteredHwnd(hwnd)    {
        return this._registeredHwnds[hwnd & 0xFFFFFFFF] := hwnd
    }
    _hasRegisteredHwnd(hwnd)    {
        return this._registeredHwnds.hasKey(hwnd & 0xFFFFFFFF)
    }
    _deleteRegisteredHwnd(hwnd)    {
        return this._registeredHwnds.delete(hwnd & 0xFFFFFFFF)
    }
    _RegisteredHwndCount    {
        get  {
            return this._registeredHwnds.count()
        }
    }
}