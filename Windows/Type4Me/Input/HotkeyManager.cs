using System.Diagnostics;
using System.Runtime.InteropServices;
using Type4Me.Models;
using Type4Me.NativeMethods;
using Type4Me.Services;

namespace Type4Me.Input;

/// <summary>
/// Global keyboard hook for hotkey detection.
/// Supports hold (press=start, release=stop) and toggle (press=start, press=stop) styles.
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    // ── Events ─────────────────────────────────────────────

    /// <summary>Fired when a mode's hotkey triggers recording start.</summary>
    public event Action<ProcessingMode>? OnStartRecording;

    /// <summary>Fired when a mode's hotkey triggers recording stop.</summary>
    public event Action<ProcessingMode>? OnStopRecording;

    /// <summary>Fired when a different mode's key is pressed during recording.</summary>
    public event Action<ProcessingMode>? OnCrossModeStop;

    // ── State ──────────────────────────────────────────────

    private IntPtr _hookId;
    private Win32.LowLevelKeyboardProc? _hookProc;
    private ProcessingMode[] _modes = [];
    private ProcessingMode? _activeMode;
    private bool _isRecording;
    private bool _isSuppressed;
    private DateTime _holdStartTime;

    private const int MaxHoldSeconds = 120;

    // ── Configuration ──────────────────────────────────────

    /// <summary>Update the list of modes with their hotkey bindings.</summary>
    public void SetModes(ProcessingMode[] modes) => _modes = modes;

    /// <summary>Suppress all hotkey handling (during hotkey recording in Settings).</summary>
    public bool IsSuppressed
    {
        get => _isSuppressed;
        set => _isSuppressed = value;
    }

    /// <summary>Notify the manager that recording has started.</summary>
    public void SetRecording(bool recording, ProcessingMode? mode = null)
    {
        _isRecording = recording;
        _activeMode = recording ? mode : null;
    }

    // ── Install / Remove Hook ──────────────────────────────

    public void Install()
    {
        _hookProc = HookCallback;
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _hookId = Win32.SetWindowsHookExW(
            Win32.WH_KEYBOARD_LL,
            _hookProc,
            Win32.GetModuleHandleW(module.ModuleName),
            0);

        if (_hookId == IntPtr.Zero)
            DebugFileLogger.Log($"[HotkeyManager] Failed to install hook: {Marshal.GetLastWin32Error()}");
        else
            DebugFileLogger.Log("[HotkeyManager] Hook installed");
    }

    public void Uninstall()
    {
        if (_hookId != IntPtr.Zero)
        {
            Win32.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            DebugFileLogger.Log("[HotkeyManager] Hook uninstalled");
        }
    }

    // ── Hook Callback ──────────────────────────────────────

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && !_isSuppressed)
        {
            var kbd = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
            int vk = (int)kbd.vkCode;
            int msg = (int)wParam;
            bool isDown = msg == Win32.WM_KEYDOWN || msg == Win32.WM_SYSKEYDOWN;
            bool isUp = msg == Win32.WM_KEYUP || msg == Win32.WM_SYSKEYUP;

            if (isDown || isUp)
                HandleKey(vk, isDown);
        }

        return Win32.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private void HandleKey(int vk, bool isDown)
    {
        // Find matching mode
        foreach (var mode in _modes)
        {
            if (mode.HotkeyCode == null) continue;
            if (mode.HotkeyCode != vk) continue;

            // Check modifiers if required
            if (mode.HotkeyModifiers is > 0)
            {
                uint currentMods = GetCurrentModifiers();
                if ((currentMods & mode.HotkeyModifiers.Value) != mode.HotkeyModifiers.Value)
                    continue;
            }

            if (isDown)
            {
                if (_isRecording)
                {
                    if (_activeMode?.Id == mode.Id)
                    {
                        // Same mode: toggle off (for toggle style)
                        if (mode.HotkeyStyle == HotkeyStyle.Toggle)
                            OnStopRecording?.Invoke(mode);
                    }
                    else
                    {
                        // Different mode: cross-mode stop
                        OnCrossModeStop?.Invoke(mode);
                    }
                }
                else
                {
                    // Start recording
                    _holdStartTime = DateTime.Now;
                    OnStartRecording?.Invoke(mode);
                }
            }
            else // key up
            {
                if (_isRecording && _activeMode?.Id == mode.Id && mode.HotkeyStyle == HotkeyStyle.Hold)
                {
                    // Hold mode: release = stop
                    OnStopRecording?.Invoke(mode);
                }
            }
            return;
        }
    }

    private static uint GetCurrentModifiers()
    {
        uint mods = 0;
        if ((Win32.GetAsyncKeyState(Win32.VK_CONTROL) & 0x8000) != 0) mods |= Win32.MOD_CONTROL;
        if ((Win32.GetAsyncKeyState(Win32.VK_SHIFT) & 0x8000) != 0) mods |= Win32.MOD_SHIFT;
        if ((Win32.GetAsyncKeyState(Win32.VK_MENU) & 0x8000) != 0) mods |= Win32.MOD_ALT;
        if ((Win32.GetAsyncKeyState(Win32.VK_LWIN) & 0x8000) != 0 ||
            (Win32.GetAsyncKeyState(Win32.VK_RWIN) & 0x8000) != 0) mods |= Win32.MOD_WIN;
        return mods;
    }

    public void Dispose() => Uninstall();
}
