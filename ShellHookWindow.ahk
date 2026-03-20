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
        SHELLHOOKWINDOW_VERSION := "2.0.0"
    }
}
class ShellHookWindow
{
    static ensureOnMessage(callback, maxThreads := 1) => this.register(A_ScriptHwnd)
            ? this.onMessage(callback, maxThreads)
            : ""
    ;-------------------------------------------------------
    static WM_SHELLHOOKMESSAGE    {
        get  {
            static msgNumber := dllCall("User32.dll\RegisterWindowMessageW", "WStr","SHELLHOOK", "UInt")
            return msgNumber
        }
    }
    static register(hwnd := A_ScriptHwnd)    {
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
    static deregister(hwnd := A_ScriptHwnd)    {
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
    static isRegistered(hwnd := A_ScriptHwnd) => this._hasRegisteredHwnd(hwnd)
    static onMessage(callback, maxThreads := 1) => onMessage(this.WM_SHELLHOOKMESSAGE, callback, maxThreads)
    ;-------------------------------------------------------
    static _objbmOnExiting := objBindMethod(this, "_onExiting")
    static _onExiting(*)    {
        hwnds := this._registeredHwnds.clone(), this._registeredHwnds := Map()
        for _,hwnd in hwnds
            dllCall("User32.dll\DeregisterShellHookWindow", "Ptr",hwnd, "Int")
    }
    ;-------------------------------------------------------
    static _registeredHwnds := Map()
    static _setRegisteredHwnd(hwnd)     => this._registeredHwnds[hwnd & 0xFFFFFFFF] := hwnd
    static _hasRegisteredHwnd(hwnd)     => this._registeredHwnds.has(hwnd & 0xFFFFFFFF)
    static _deleteRegisteredHwnd(hwnd)  => this._registeredHwnds.delete(hwnd & 0xFFFFFFFF)
    static _RegisteredHwndCount         => this._registeredHwnds.Count
}