param()

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$clangCommand = Get-Command clang.exe -ErrorAction SilentlyContinue
$windresCommand = Get-Command windres.exe -ErrorAction SilentlyContinue
$clang = if ($clangCommand) { $clangCommand.Source } else { 'C:\msys64\clang64\bin\clang.exe' }
$windres = if ($windresCommand) { $windresCommand.Source } else { 'C:\msys64\clang64\bin\windres.exe' }
$source = Join-Path $root 'src\BigDogBark.c'
$sound = Join-Path $root 'assets\codex-complete.mp3'
$resource = Join-Path $root 'app.rc'
$resourceObject = Join-Path $root 'app.res.o'
$releaseDirectory = Join-Path $root 'release'
$output = Join-Path $releaseDirectory 'BigDogBark.exe'

New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null

if (-not (Test-Path -LiteralPath $clang -PathType Leaf)) {
  throw "clang not found: $clang"
}
if (-not (Test-Path -LiteralPath $windres -PathType Leaf)) {
  throw "windres not found: $windres"
}

& $windres --input $resource --output $resourceObject --target pe-x86-64 --include-dir $root
if ($LASTEXITCODE -ne 0) {
  throw "Resource compilation failed with exit code $LASTEXITCODE."
}

$arguments = @(
  '-std=c11',
  '-Os',
  '-ffunction-sections',
  '-fdata-sections',
  '-s',
  '-municode',
  '-mwindows',
  '-static',
  '-Wl,--gc-sections',
  '-o', $output,
  $source,
  $resourceObject,
  '-lole32',
  '-loleaut32',
  '-luuid',
  '-lshell32',
  '-lshlwapi',
  '-lwinmm',
  '-luser32',
  '-ladvapi32'
)

& $clang $arguments
if ($LASTEXITCODE -ne 0) {
  throw "Compilation failed with exit code $LASTEXITCODE."
}

if (Test-Path -LiteralPath $resourceObject) {
  Remove-Item -LiteralPath $resourceObject -Force
}

Copy-Item -LiteralPath $sound -Destination (Join-Path $releaseDirectory 'codex-complete.mp3') -Force

$archive = Join-Path $releaseDirectory 'BigDogBark-portable.zip'
if (Test-Path -LiteralPath $archive) {
  Remove-Item -LiteralPath $archive -Force
}
Compress-Archive -Path (Join-Path $releaseDirectory 'BigDogBark.exe'),(Join-Path $releaseDirectory 'codex-complete.mp3') -DestinationPath $archive

Remove-Item -LiteralPath (Join-Path $releaseDirectory 'BigDogBark.exe'),(Join-Path $releaseDirectory 'codex-complete.mp3') -Force

Get-Item -LiteralPath $archive | Select-Object FullName, Length, LastWriteTime
