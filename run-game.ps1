[CmdletBinding()]
param(
    [switch]$Editor,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$repositoryRoot = $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot "Interregnum.sln"
$projectPath = Join-Path $repositoryRoot "src\Interregnum.Game"
$bundledGodotPath = Join-Path $repositoryRoot ".tools\godot-4.7.2\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe"

if (-not $SkipBuild) {
    & dotnet restore $solutionPath
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed with exit code $LASTEXITCODE."
    }

    & dotnet build $solutionPath --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE."
    }
}

if (Test-Path -LiteralPath $bundledGodotPath -PathType Leaf) {
    $godotPath = $bundledGodotPath
}
else {
    $godotCommand = Get-Command godot -ErrorAction SilentlyContinue
    if ($null -eq $godotCommand) {
        throw "Godot 4.7.2 .NET was not found in '.tools' or on PATH. Install the .NET build of Godot and try again."
    }

    $godotPath = $godotCommand.Source
}

$godotArguments = @("--path", $projectPath)
if ($Editor) {
    $godotArguments += "--editor"
}

& $godotPath @godotArguments
if ($LASTEXITCODE -ne 0) {
    throw "Godot exited with code $LASTEXITCODE."
}
