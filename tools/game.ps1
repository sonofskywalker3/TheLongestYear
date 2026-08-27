#requires -Version 7
<#
.SYNOPSIS
    Drive the running Stardew window: focus, click, keys, walking, screenshots.

.DESCRIPTION
    Replaces the ad-hoc test-output/click.ps1 + printwindow.ps1 pair. Two facts drove the rewrite
    (2026-08-26 session, where an automated repro could not be set up at all and the time was lost
    to a game that looked broken but was only asleep):

    1. SDV PAUSES WHEN UNFOCUSED. A `debug warp` queued over the SMAPI console does not run until
       the game updates, and PrintWindow keeps returning the last rendered frame - so a stalled
       game is indistinguishable from a command that failed. Every action here focuses first.

    2. SetForegroundWindow ALONE DOES NOT WORK, AND FAILS SILENTLY. Windows' foreground lock
       ignores it when the caller does not already own the foreground. Keyboard input then goes
       nowhere: XNA reads the keyboard via GetKeyboardState, which is per-input-queue, so an
       unfocused window sees no keys however they are synthesised. That is exactly why key presses
       "did not move the farmer" while mouse clicks worked - a click lands on whatever window is
       under the cursor and focuses it as a side effect. Focus() attaches our thread's input queue
       to the foreground window's thread, which lifts the lock, then VERIFIES with
       GetForegroundWindow and retries. If it cannot focus, this script exits non-zero rather than
       reporting a result it did not achieve.

.EXAMPLE
    pwsh -File tools/game.ps1 -Focus
    pwsh -File tools/game.ps1 -Click 660,540
    pwsh -File tools/game.ps1 -Key Escape
    pwsh -File tools/game.ps1 -Walk right -Ms 1500
    pwsh -File tools/game.ps1 -Shot "test-output/pierre.png"
#>
param(
    [switch]$Focus,
    [string]$Click = "",
    [string]$Move = "",
    [string]$Key = "",
    [string]$Walk = "",
    [int]$Ms = 800,
    [string]$Text = "",
    [string]$Shot = ""
)

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public class Game {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr pid);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern uint SendInput(uint n, INPUT[] inputs, int size);

    public delegate bool EnumProc(IntPtr h, IntPtr p);
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Explicit)] public struct IUNION { [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public IUNION u; }

    // The GAME window, not the SMAPI console: both carry "Stardew Valley" in the title, but the
    // MonoGame window is class SDL_app and the console is ConsoleWindowClass / CASCADIA_*.
    public static IntPtr Find() {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, p) => {
            if (!IsWindowVisible(h)) return true;
            var t = new StringBuilder(256); GetWindowText(h, t, 256);
            var c = new StringBuilder(256); GetClassName(h, c, 256);
            if (t.ToString().Contains("Stardew Valley") && c.ToString() == "SDL_app") { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    // Lifts the foreground lock by attaching our input queue to the current foreground thread,
    // then verifies. Silent failure here is what made the game look broken for a whole session.
    public static bool Focus(IntPtr h) {
        for (int attempt = 0; attempt < 5; attempt++) {
            if (GetForegroundWindow() == h) return true;
            IntPtr fg = GetForegroundWindow();
            uint us = GetCurrentThreadId();
            uint them = fg == IntPtr.Zero ? us : GetWindowThreadProcessId(fg, IntPtr.Zero);
            if (them != us) AttachThreadInput(us, them, true);
            ShowWindow(h, 9);            // SW_RESTORE, in case it is minimised
            BringWindowToTop(h);
            SetForegroundWindow(h);
            if (them != us) AttachThreadInput(us, them, false);
            System.Threading.Thread.Sleep(180);
            if (GetForegroundWindow() == h) return true;
        }
        return GetForegroundWindow() == h;
    }

    public static void KeyDown(ushort vk) {
        var i = new INPUT[1];
        i[0].type = 1; i[0].u.ki = new KEYBDINPUT { wVk = vk, dwFlags = 0 };
        SendInput(1, i, Marshal.SizeOf(typeof(INPUT)));
    }
    public static void KeyUp(ushort vk) {
        var i = new INPUT[1];
        i[0].type = 1; i[0].u.ki = new KEYBDINPUT { wVk = vk, dwFlags = 2 };
        SendInput(1, i, Marshal.SizeOf(typeof(INPUT)));
    }
    public static void TypeChar(char c) {
        var i = new INPUT[2];
        i[0].type = 1; i[0].u.ki = new KEYBDINPUT { wScan = (ushort)c, dwFlags = 4 };
        i[1].type = 1; i[1].u.ki = new KEYBDINPUT { wScan = (ushort)c, dwFlags = 4 | 2 };
        SendInput(2, i, Marshal.SizeOf(typeof(INPUT)));
    }

}
"@ -ReferencedAssemblies System.Threading.Thread -ErrorAction Stop

if (-not ("Game" -as [type])) {
    Write-Error "Add-Type did not produce [Game] - every action below would silently no-op while reporting success. Aborting."
    exit 3
}

[void][Game]::SetProcessDpiAwarenessContext([IntPtr](-4))   # per-monitor v2: the game sits at X~-2880
$h = [Game]::Find()
if ($h -eq [IntPtr]::Zero) { Write-Error "game window not found (is SMAPI running?)"; exit 1 }

# EVERYTHING focuses first: an unfocused SDV is a paused SDV.
if (-not [Game]::Focus($h)) {
    Write-Error "could not bring the game to the foreground - input would be swallowed and the game stays paused. Aborting rather than reporting a false result."
    exit 2
}
Start-Sleep -Milliseconds 120

$VK = @{ escape=0x1B; esc=0x1B; enter=0x0D; return=0x0D; space=0x20; tab=0x09; backspace=0x08;
         up=0x26; down=0x28; left=0x25; right=0x27;
         w=0x57; a=0x41; s=0x53; d=0x44; e=0x45; y=0x59; n=0x4E; f=0x46; c=0x43; i=0x49; x=0x58 }

if ($Key -ne "") {
    if (-not $VK.ContainsKey($Key.ToLower())) { Write-Error "unsupported key '$Key'"; exit 1 }
    [Game]::KeyDown([uint16]$VK[$Key.ToLower()]); Start-Sleep -Milliseconds 60
    [Game]::KeyUp([uint16]$VK[$Key.ToLower()])
    "key: $Key"
}

if ($Walk -ne "") {
    # Walking needs a HELD key. A tap moves the farmer a pixel or two and reads as "nothing moved".
    $dir = $Walk.ToLower()
    $map = @{ right="d"; left="a"; up="w"; down="s" }
    if ($map.ContainsKey($dir)) { $dir = $map[$dir] }
    if (-not $VK.ContainsKey($dir)) { Write-Error "unsupported direction '$Walk'"; exit 1 }
    [Game]::KeyDown([uint16]$VK[$dir])
    Start-Sleep -Milliseconds $Ms
    [Game]::KeyUp([uint16]$VK[$dir])
    "walk: $Walk for $Ms ms"
}

if ($Text -ne "") { foreach ($c in $Text.ToCharArray()) { [Game]::TypeChar($c); Start-Sleep -Milliseconds 30 }; "typed: $Text" }

if ($Click -ne "" -or $Move -ne "") {
    # pwsh -File passes every argument as a STRING, so "-Click 707,530" arrives as one string and
    # an [int[]] cast silently produces 707530 - a click at nonsense coordinates that still reports
    # success. Parse it here instead.
    $raw = if ($Click -ne "") { $Click } else { $Move }
    $parts = $raw -split '[,x ]+' | Where-Object { $_ -ne "" }
    if ($parts.Count -ne 2) { Write-Error "coordinates must be 'x,y' (got '$raw')"; exit 1 }
    $xy = @([int]$parts[0], [int]$parts[1])
    $p = New-Object Game+POINT; $p.X = $xy[0]; $p.Y = $xy[1]
    [void][Game]::ClientToScreen($h, [ref]$p)
    [void][Game]::SetCursorPos($p.X, $p.Y)
    Start-Sleep -Milliseconds 120
    if ($Click -ne "") {
        [Game]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 60
        [Game]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
        "click: client ($($xy[0]),$($xy[1])) screen ($($p.X),$($p.Y))"
    } else { "move: client ($($xy[0]),$($xy[1]))" }
}

if ($Shot -ne "") {
    # Capture lives in tools/screenshot.ps1 (it needs System.Drawing); this script stays
    # input-only. Focus already happened above, so the frame is live rather than last-drawn.
    $full = if ([System.IO.Path]::IsPathRooted($Shot)) { $Shot } else { Join-Path (Get-Location) $Shot }
    $script = Join-Path $PSScriptRoot "screenshot.ps1"
    & pwsh -NoProfile -File $script $full | Out-Null
    if (-not (Test-Path $full)) { Write-Error "screenshot did not land at $full"; exit 4 }
    "shot: $full"
}

if (-not ($Key -or $Walk -or $Text -or ($Click -ne "") -or ($Move -ne "") -or $Shot)) { "focused: ok" }
