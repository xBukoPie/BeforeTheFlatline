param(
    [string]$GodotPath = $env:GODOT_PATH
)

$ErrorActionPreference = "Stop"

$ProjectRoot = $PSScriptRoot
$SolutionPath = Join-Path $ProjectRoot "BeforetheFlatline.sln"
$ExportDirectory = Join-Path $ProjectRoot "builds\windows"
$ExportRelativePath = "builds/windows/BeforeTheFlatline.exe"
$ExportAbsolutePath = Join-Path $ExportDirectory "BeforeTheFlatline.exe"

if (-not (Test-Path -LiteralPath (Join-Path $ProjectRoot "project.godot"))) {
    throw "project.godot was not found. Run this script from the Godot project folder."
}

if ([string]::IsNullOrWhiteSpace($GodotPath)) {
    $GodotCommand = Get-Command "godot" -ErrorAction SilentlyContinue
    if ($null -eq $GodotCommand) {
        throw "Set GODOT_PATH or pass -GodotPath to the Godot 4.7.2 .NET editor executable."
    }

    $GodotPath = $GodotCommand.Source
}

if (-not (Test-Path -LiteralPath $GodotPath -PathType Leaf)) {
    throw "Godot executable not found: $GodotPath"
}

$GodotItem = Get-Item -LiteralPath $GodotPath
if ($GodotItem.LinkType -eq "SymbolicLink" -and $GodotItem.Target) {
    $GodotTarget = [string]$GodotItem.Target
    if (-not [System.IO.Path]::IsPathRooted($GodotTarget)) {
        $GodotTarget = Join-Path $GodotItem.DirectoryName $GodotTarget
    }
    $GodotPath = $GodotTarget
}

if ([System.IO.Path]::GetExtension($GodotPath) -ne ".exe") {
    $ExecutableCandidate = "$GodotPath.exe"
    if (Test-Path -LiteralPath $ExecutableCandidate -PathType Leaf) {
        $GodotPath = $ExecutableCandidate
    }
}

# The setup-godot CI action exposes an extensionless symlink; locate the real editor binary.
if ([System.IO.Path]::GetExtension($GodotPath) -ne ".exe") {
    $GodotInstallRoot = Join-Path $env:USERPROFILE "godot"
    if (Test-Path -LiteralPath $GodotInstallRoot -PathType Container) {
        $GodotExecutable = Get-ChildItem -LiteralPath $GodotInstallRoot -Filter "Godot_v*_mono_win64.exe" -Recurse -File -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($null -ne $GodotExecutable) {
            $GodotPath = $GodotExecutable.FullName
        }
    }
}

if ([System.IO.Path]::GetExtension($GodotPath) -ne ".exe") {
    throw "The selected Godot path is not a Windows executable: $GodotPath"
}

$GodotPath = (Resolve-Path -LiteralPath $GodotPath).Path
$GodotVersion = (& $GodotPath --version 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Could not query the Godot version from '$GodotPath'."
}

if ($GodotVersion -notmatch '^4\.7\.2' -or $GodotVersion -notmatch '(?i)mono') {
    throw "This project requires the Godot 4.7.2 .NET editor. Detected: $GodotVersion"
}

$DotnetSdks = & dotnet --list-sdks 2>&1
if ($LASTEXITCODE -ne 0) {
    throw ".NET SDK was not found. Install the .NET 8 SDK and retry."
}

$HasNet8Sdk = $DotnetSdks | Where-Object { $_ -match '^\s*8\.\d+\.\d+' }
if (-not $HasNet8Sdk) {
    throw "This project targets net8.0. Install the .NET 8 SDK. Detected SDKs: $($DotnetSdks -join ', ')"
}

if (-not (Test-Path -LiteralPath $SolutionPath -PathType Leaf)) {
    throw "Solution file was not found: $SolutionPath"
}

New-Item -ItemType Directory -Path $ExportDirectory -Force | Out-Null

Push-Location $ProjectRoot
try {
    Write-Host "Godot: $GodotVersion"
    Write-Host "Importing resources and building the Godot C# solution..."
    & $GodotPath --headless --editor --path $ProjectRoot --build-solutions --quit
    if ($LASTEXITCODE -ne 0) {
        throw "Godot resource import or C# solution build failed."
    }

    Write-Host "Building C# solution (ExportRelease)..."
    & dotnet build $SolutionPath --configuration ExportRelease
    if ($LASTEXITCODE -ne 0) {
        throw "The C# ExportRelease build failed."
    }

    Write-Host "Exporting Windows Desktop release..."
    & $GodotPath --headless --path $ProjectRoot --verbose --export-release "Windows Desktop" $ExportRelativePath
    if ($LASTEXITCODE -ne 0) {
        throw "Godot Windows export failed. Confirm the matching 4.7.2 .NET export templates are installed."
    }

    if (-not (Test-Path -LiteralPath $ExportAbsolutePath -PathType Leaf)) {
        throw "Export command completed, but the executable was not created: $ExportAbsolutePath"
    }

    $Executable = Get-Item -LiteralPath $ExportAbsolutePath
    $Hash = Get-FileHash -LiteralPath $ExportAbsolutePath -Algorithm SHA256

    Write-Host ""
    Write-Host "Build completed:"
    Write-Host "  $($Executable.FullName)"
    Write-Host "  Size: $([Math]::Round($Executable.Length / 1MB, 2)) MB"
    Write-Host "  SHA-256: $($Hash.Hash)"
}
finally {
    Pop-Location
}
