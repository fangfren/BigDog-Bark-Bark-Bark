param(
  [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source = Join-Path $root 'src\BigDogBark.cs'
$sound = Join-Path $root 'assets\codex-complete.wav'
$iconOn = Join-Path $root 'assets\app-on.ico'
$iconOff = Join-Path $root 'assets\app-off.ico'
$output = Join-Path $root 'BigDogBark.exe'

foreach ($path in @($csc, $source, $sound, $iconOn, $iconOff)) {
  if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
    throw "Required file not found: $path"
  }
}

Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

$presentationCore = [System.Windows.Media.MediaPlayer].Assembly.Location
$windowsBase = [System.Windows.Threading.Dispatcher].Assembly.Location

$arguments = @(
  '/nologo',
  '/target:winexe',
  '/platform:anycpu',
  '/optimize+',
  "/win32icon:$iconOn",
  "/out:$output",
  "/resource:$sound,BarkSound",
  "/resource:$iconOn,IconOn",
  "/resource:$iconOff,IconOff",
  '/reference:System.dll',
  '/reference:System.Core.dll',
  '/reference:System.Drawing.dll',
  '/reference:System.Windows.Forms.dll',
  "/reference:$presentationCore",
  "/reference:$windowsBase",
  $source
)

& $csc $arguments
if ($LASTEXITCODE -ne 0) {
  throw "Compilation failed with exit code $LASTEXITCODE."
}

Get-Item -LiteralPath $output | Select-Object FullName, Length, LastWriteTime
