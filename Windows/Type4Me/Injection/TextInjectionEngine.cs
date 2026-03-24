using System.Runtime.InteropServices;
using Type4Me.Models;
using Type4Me.NativeMethods;
using Type4Me.Services;

namespace Type4Me.Injection;

/// <summary>
/// Injects text into the focused application via clipboard + Ctrl+V.
/// </summary>
public sealed class TextInjectionEngine
{
    /// <summary>
    /// Inject text at the cursor position. Uses clipboard + SendInput Ctrl+V.
    /// </summary>
    public InjectionOutcome Inject(string text)
    {
        if (string.IsNullOrEmpty(text))
            return InjectionOutcome.CopiedToClipboard;

        try
        {
            // Save and restore clipboard content is skipped for simplicity
            // (matches macOS behavior which also replaces clipboard)
            CopyToClipboard(text);

            // Small delay to ensure clipboard is ready
            Thread.Sleep(50);

            // Simulate Ctrl+V
            SimulateCtrlV();

            DebugFileLogger.Log($"[Injection] Injected {text.Length} chars via Ctrl+V");
            return InjectionOutcome.Inserted;
        }
        catch (Exception ex)
        {
            DebugFileLogger.Log($"[Injection] Failed: {ex.Message}, text in clipboard");
            return InjectionOutcome.CopiedToClipboard;
        }
    }

    /// <summary>Copy text to system clipboard.</summary>
    public void CopyToClipboard(string text)
    {
        // Must run on STA thread for clipboard access
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            System.Windows.Clipboard.SetText(text);
        }
        else
        {
            var thread = new Thread(() => System.Windows.Clipboard.SetText(text));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }
    }

    /// <summary>Simulate Ctrl+V keypress using SendInput.</summary>
    private static void SimulateCtrlV()
    {
        var inputs = new Win32.INPUT[4];

        // Key down: Ctrl
        inputs[0].type = Win32.INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = (ushort)Win32.VK_CONTROL;

        // Key down: V
        inputs[1].type = Win32.INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = 0x56; // VK_V

        // Key up: V
        inputs[2].type = Win32.INPUT_KEYBOARD;
        inputs[2].u.ki.wVk = 0x56;
        inputs[2].u.ki.dwFlags = Win32.KEYEVENTF_KEYUP;

        // Key up: Ctrl
        inputs[3].type = Win32.INPUT_KEYBOARD;
        inputs[3].u.ki.wVk = (ushort)Win32.VK_CONTROL;
        inputs[3].u.ki.dwFlags = Win32.KEYEVENTF_KEYUP;

        Win32.SendInput(4, inputs, Marshal.SizeOf<Win32.INPUT>());
    }
}
