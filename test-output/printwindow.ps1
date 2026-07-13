# Screenshot the running Stardew (SMAPI) window via PrintWindow flag 2 (PW_RENDERFULLCONTENT).
# Captures the window's own GPU render even when occluded. Matches the GAME window by title
# containing "Stardew Valley" AND window class "SDL_app" — a title-only match grabs the SMAPI
# console first (both windows have "Stardew Valley"+"SMAPI" in the title; the console is
# ConsoleWindowClass / CASCADIA_HOSTING_WINDOW_CLASS, the MonoGame window is SDL_app).
# Usage: pwsh -NoProfile -File printwindow.ps1 <out.png>
param([string]$Out = "win-screen.png")

Add-Type @"
using System;
using System.Drawing;
using System.Runtime.InteropServices;
public class WinCap {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    public delegate bool EnumProc(IntPtr h, IntPtr p);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public static IntPtr Find() {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, p) => {
            if (!IsWindowVisible(h)) return true;
            var sb = new System.Text.StringBuilder(256);
            GetWindowText(h, sb, 256);
            var cls = new System.Text.StringBuilder(256);
            GetClassName(h, cls, 256);
            if (sb.ToString().Contains("Stardew Valley") && cls.ToString() == "SDL_app") { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
"@ -ReferencedAssemblies System.Drawing

# DPI-aware BEFORE any window metrics: at >100% display scaling GetWindowRect otherwise
# returns logical pixels while PrintWindow renders physical — the bitmap comes out as a
# cropped top-left corner (seen 2026-07-13 at 150% scaling: 1295x757 crop of a 1920x1080 game).
[void][WinCap]::SetProcessDPIAware()
$h = [WinCap]::Find()
if ($h -eq [IntPtr]::Zero) { Write-Error "Stardew SMAPI window not found"; exit 1 }
$r = New-Object WinCap+RECT
[void][WinCap]::GetWindowRect($h, [ref]$r)
$w = $r.Right - $r.Left; $ht = $r.Bottom - $r.Top
$bmp = New-Object System.Drawing.Bitmap $w, $ht
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
[void][WinCap]::PrintWindow($h, $hdc, 2)
$g.ReleaseHdc($hdc); $g.Dispose()
$path = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) $Out
$bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "Saved $path ($w x $ht)"
