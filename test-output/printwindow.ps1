# Screenshot the running Stardew (SMAPI) window via PrintWindow flag 2 (PW_RENDERFULLCONTENT).
# Captures the window's own GPU render even when occluded. Matches the window by title containing
# both "Stardew Valley" and "SMAPI". Usage: pwsh -NoProfile -File printwindow.ps1 <out.png>
param([string]$Out = "win-screen.png")

Add-Type @"
using System;
using System.Drawing;
using System.Runtime.InteropServices;
public class WinCap {
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    public delegate bool EnumProc(IntPtr h, IntPtr p);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public static IntPtr Find() {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, p) => {
            if (!IsWindowVisible(h)) return true;
            var sb = new System.Text.StringBuilder(256);
            GetWindowText(h, sb, 256);
            string t = sb.ToString();
            if (t.Contains("Stardew Valley") && t.Contains("SMAPI")) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
"@ -ReferencedAssemblies System.Drawing

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
