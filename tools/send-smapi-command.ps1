#requires -Version 7
<#
.SYNOPSIS
    Inject one or more console commands into the RUNNING SMAPI window's input buffer on PC.

.DESCRIPTION
    Types commands into the live SMAPI console without stealing focus. `AppActivate`+`SendKeys`
    does NOT work here: the Windows foreground-lock keeps focus on whatever window the user has
    active, so the keystrokes never reach SMAPI. Instead we attach to SMAPI's console and write
    straight into its input buffer with WriteConsoleInput, which is focus-independent.

    In-world commands (tly_setday, tly_additem, tly_buyupgrade, tly_addjp, …) need a save loaded
    (Context.IsWorldReady) or the handler logs "Load a save first." Verify results by reading
    %APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt (this script does not read the game state).

    Gotchas baked in (each one bit a prior agent):
      - CreateFileW("CONIN$", 0xC0000000, …): the access flag 0xC0000000 overflows Int32, so it
        MUST be passed as [uint32]3221225472 or the handle comes back null/invalid.
      - INPUT_RECORD is LayoutKind.Explicit: EventType at offset 0, KeyEvent at offset 4 (4-byte
        alignment). KEY_EVENT_RECORD is Sequential.
      - PowerShell value-type structs: build each KEY_EVENT_RECORD / INPUT_RECORD as a complete
        local and assign it into the array ($arr[$i] = $rec). Mutating $arr[$i].KeyEvent.field in
        place silently no-ops (you'd edit a copy).
      - ~200ms between lines so SMAPI's reader consumes each line before the next (matters for
        ordered chains like tly_addjp -> tly_buyupgrade that enforce prerequisites).
      - FreeConsole() before AttachConsole() and again at the end, so this process detaches cleanly.

.EXAMPLE
    pwsh -NoProfile -File send-smapi-command.ps1 "tly_setday 28"

.EXAMPLE
    pwsh -NoProfile -File send-smapi-command.ps1 "tly_addjp 5000" "tly_buyupgrade keep_hoe_1"

.NOTES
    Verified 2026-06-08 (set Summer 28 for a loop-reset playtest). See user memory
    smapi-console-input-injection.md.
#>
param(
    [Parameter(Mandatory = $true, ValueFromRemainingArguments = $true)]
    [string[]]$Commands,

    [string]$ProcessName = 'StardewModdingAPI',

    # Milliseconds to wait between consecutive lines so SMAPI processes them in order.
    [int]$DelayMs = 200
)

$ErrorActionPreference = 'Stop'

$src = @'
using System;
using System.Runtime.InteropServices;
public static class ConInj {
    [DllImport("kernel32.dll", SetLastError=true)] public static extern bool FreeConsole();
    [DllImport("kernel32.dll", SetLastError=true)] public static extern bool AttachConsole(uint pid);
    [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)]
    public static extern IntPtr CreateFileW(string n, uint a, uint s, IntPtr sa, uint c, uint f, IntPtr t);
    [DllImport("kernel32.dll", SetLastError=true)]
    public static extern bool WriteConsoleInputW(IntPtr h, INPUT_RECORD[] b, uint n, out uint w);
    [StructLayout(LayoutKind.Sequential)] public struct KEY_EVENT_RECORD {
        public int bKeyDown; public ushort wRepeatCount; public ushort wVirtualKeyCode;
        public ushort wVirtualScanCode; public ushort UnicodeChar; public uint dwControlKeyState; }
    [StructLayout(LayoutKind.Explicit)] public struct INPUT_RECORD {
        [FieldOffset(0)] public ushort EventType; [FieldOffset(4)] public KEY_EVENT_RECORD KeyEvent; }
}
'@
Add-Type -TypeDefinition $src

$proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { throw "No '$ProcessName' process found — is SMAPI running?" }

function New-InputRecords([string]$line) {
    $full = $line + "`r"
    $arr = New-Object 'ConInj+INPUT_RECORD[]' ($full.Length * 2)
    $i = 0
    foreach ($ch in $full.ToCharArray()) {
        foreach ($down in 1, 0) {
            $ke = New-Object 'ConInj+KEY_EVENT_RECORD'
            $ke.bKeyDown = $down
            $ke.wRepeatCount = 1
            $ke.UnicodeChar = [uint16][char]$ch
            $rec = New-Object 'ConInj+INPUT_RECORD'
            $rec.EventType = 1
            $rec.KeyEvent = $ke
            $arr[$i] = $rec
            $i++
        }
    }
    return $arr
}

# Detach our own console, attach SMAPI's, grab its input handle (CONIN$).
[ConInj]::FreeConsole() | Out-Null
$attached = [ConInj]::AttachConsole([uint32]$proc.Id)
$hConin = [ConInj]::CreateFileW('CONIN$', [uint32]3221225472, 3, [IntPtr]::Zero, 3, 0, [IntPtr]::Zero)
$handleOk = ($hConin -ne [IntPtr]::Zero -and $hConin -ne [IntPtr](-1))

$sent = 0
if ($handleOk) {
    foreach ($cmd in $Commands) {
        $records = New-InputRecords $cmd
        $written = 0
        if ([ConInj]::WriteConsoleInputW($hConin, $records, [uint32]$records.Length, [ref]$written)) { $sent++ }
        Start-Sleep -Milliseconds $DelayMs
    }
}

# Restore our own console state.
[ConInj]::FreeConsole() | Out-Null

# stdout is a captured pipe (not the console), so this still reports after detach.
"attached=$attached handleOk=$handleOk sent=$sent/$($Commands.Count) pid=$($proc.Id)"
if (-not $handleOk) { throw 'CONIN$ handle was invalid — check the [uint32]3221225472 access-flag cast.' }
