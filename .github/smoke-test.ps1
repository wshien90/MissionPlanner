# Smoke test on Windows: starts FSEMIMissionPlanner.exe from the build output, checks that it is still
# running after a while and saves screenshots. Fails when the app exits, printing the crash from the
# Windows event log. -Language sets the interface language (e.g. vi-VN) in the settings first.
param(
    [string]$AppDir = "bin\Release\net461",
    [string]$Out = "smoke",
    [int[]]$ShotsAt = @(20, 45, 90),
    [string]$Language = ""
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force $Out | Out-Null
Add-Type -AssemblyName System.Windows.Forms, System.Drawing

function Save-Screen([string]$name) {
    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bitmap = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
    $bitmap.Save((Join-Path $Out $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
    Write-Host "Saved $name"
}

Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class SmokeWindows {
    delegate bool EnumProc(IntPtr hwnd, IntPtr lparam);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc, IntPtr lparam);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    public struct Rect { public int Left, Top, Right, Bottom; }
    // largest visible top-level window of a process: x, y, width, height and handle
    public static long[] Largest(int processId) {
        long[] best = null;
        EnumWindows((hwnd, l) => {
            uint pid;
            Rect r;
            GetWindowThreadProcessId(hwnd, out pid);
            if (pid == processId && IsWindowVisible(hwnd) && GetWindowRect(hwnd, out r)) {
                long w = r.Right - r.Left, h = r.Bottom - r.Top;
                if (w > 0 && h > 0 && (best == null || w * h > best[2] * best[3]))
                    best = new long[] { r.Left, r.Top, w, h, hwnd.ToInt64() };
            }
            return true;
        }, IntPtr.Zero);
        return best;
    }
    // visible top-level windows of a process, topmost first
    public static List<string> Of(int processId) {
        var list = new List<string>();
        EnumWindows((hwnd, l) => {
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            if (pid == processId && IsWindowVisible(hwnd)) {
                var title = new StringBuilder(256);
                var cls = new StringBuilder(256);
                GetWindowText(hwnd, title, 256);
                GetClassName(hwnd, cls, 256);
                list.Add("\"" + title + "\" (" + cls + ")");
            }
            return true;
        }, IntPtr.Zero);
        return list;
    }
}
"@

# small JPEG of the screen as base64 in the log, so the run can be looked at without downloading the artifact
function Write-Thumbnail([string]$name) {
    $source = [System.Drawing.Image]::FromFile((Resolve-Path (Join-Path $Out $name)))
    $width = 800
    $height = [int]($source.Height * $width / $source.Width)
    $thumb = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($thumb)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.DrawImage($source, 0, 0, $width, $height)
    $stream = New-Object System.IO.MemoryStream
    $jpeg = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq "image/jpeg" }
    $quality = New-Object System.Drawing.Imaging.EncoderParameters 1
    $quality.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality, [long]60)
    $thumb.Save($stream, $jpeg, $quality)
    $base64 = [Convert]::ToBase64String($stream.ToArray())
    Write-Host "THUMBNAIL-BEGIN $Language $name"
    for ($i = 0; $i -lt $base64.Length; $i += 2000) { Write-Host $base64.Substring($i, [Math]::Min(2000, $base64.Length - $i)) }
    Write-Host "THUMBNAIL-END"
    $graphics.Dispose(); $thumb.Dispose(); $source.Dispose(); $stream.Dispose()
}

# the app's main window drawn by itself (PrintWindow), so windows on top of it don't hide it
function Save-AppWindow([string]$name) {
    $window = [SmokeWindows]::Largest($app.Id)
    if ($window -eq $null) { return $false }
    $bitmap = New-Object System.Drawing.Bitmap ([int]$window[2]), ([int]$window[3])
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $hdc = $graphics.GetHdc()
    $ok = [SmokeWindows]::PrintWindow([IntPtr]$window[4], $hdc, 2)  # PW_RENDERFULLCONTENT
    $graphics.ReleaseHdc($hdc)
    if ($ok) { $bitmap.Save((Join-Path $Out $name), [System.Drawing.Imaging.ImageFormat]::Png); Write-Host "Saved $name" }
    $graphics.Dispose()
    $bitmap.Dispose()
    return $ok
}

function Show-Crashes([datetime]$since) {
    # .NET Runtime 1026 has the exception and stack, Application Error 1000 the faulting module
    Get-WinEvent -FilterHashtable @{ LogName = "Application"; StartTime = $since } -ErrorAction SilentlyContinue |
        Where-Object { $_.ProviderName -in ".NET Runtime", "Application Error", "Windows Error Reporting" } |
        ForEach-Object { Write-Host "---- $($_.ProviderName) $($_.Id) $($_.TimeCreated)"; Write-Host $_.Message }
}

if ($Language) {
    # the settings file the app reads: Documents\FSEMI Mission Planner\config.xml
    $dir = Join-Path ([Environment]::GetFolderPath("MyDocuments")) "FSEMI Mission Planner"
    New-Item -ItemType Directory -Force $dir | Out-Null
    $file = Join-Path $dir "config.xml"
    $xml = New-Object System.Xml.XmlDocument
    if (Test-Path $file) { $xml.Load($file) } else { $xml.LoadXml('<?xml version="1.0" encoding="utf-8"?><Config />') }
    $node = $xml.DocumentElement.SelectSingleNode("language")
    if ($node -eq $null) { $node = $xml.CreateElement("language"); [void]$xml.DocumentElement.AppendChild($node) }
    $node.InnerText = $Language
    $xml.Save($file)
    Write-Host "Language $Language set in $file"
}

# the voices the app can speak with (SAPI) and the ones it cannot see (OneCore, added in Settings)
Add-Type -AssemblyName System.Speech
$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
Write-Host "SAPI voices: $(($synth.GetInstalledVoices() | ForEach-Object { "$($_.VoiceInfo.Name) ($($_.VoiceInfo.Culture))" }) -join ', ')"
$synth.Dispose()
Write-Host "OneCore voices: $((Get-ChildItem HKLM:\SOFTWARE\Microsoft\Speech_OneCore\Voices\Tokens -ErrorAction SilentlyContinue | ForEach-Object { $_.PSChildName }) -join ', ')"

$start = Get-Date
$exe = Resolve-Path (Join-Path $AppDir "FSEMIMissionPlanner.exe")
$app = Start-Process $exe -WorkingDirectory (Split-Path $exe) -PassThru
Write-Host "Started FSEMIMissionPlanner.exe, process $($app.Id)"

foreach ($at in $ShotsAt) {
    $wait = $start.AddSeconds($at) - (Get-Date)
    if ($wait.TotalSeconds -gt 0) { Start-Sleep -Seconds $wait.TotalSeconds }
    if ($app.HasExited) { break }
    Save-Screen ("fsemi-{0:000}s.png" -f $at)
    Write-Host "Windows at $at s: $([SmokeWindows]::Of($app.Id) -join ', ')"
}

if ($app.HasExited) {
    Save-Screen "after-exit.png"
    Write-Host "FSEMIMissionPlanner.exe exited with code $($app.ExitCode) after $([int]((Get-Date) - $start).TotalSeconds) s"
    Show-Crashes $start
    exit 1
}

Write-Host "FSEMIMissionPlanner.exe still running after $($ShotsAt[-1]) s"
$windows = [SmokeWindows]::Of($app.Id)
if ($windows.Count -gt 1) {
    # a dialog left open at startup is usually a prompt a new user would hit; flag it on the run
    Write-Host "::warning title=Smoke test::$($windows.Count) windows open after $($ShotsAt[-1]) s: $($windows -join ', ')"
}
if (Save-AppWindow "app-window.png") { Write-Thumbnail "app-window.png" } else { Write-Thumbnail ("fsemi-{0:000}s.png" -f $ShotsAt[-1]) }
Show-Crashes $start
Stop-Process -Id $app.Id -Force
