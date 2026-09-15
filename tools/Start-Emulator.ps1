<#
.SYNOPSIS
    Starts the Casino Tracker Android emulator and (optionally) builds, installs and launches the app.

.DESCRIPTION
    One-stop script for testing on the PC:
      1. Creates the "CasinoTracker" virtual device on first use.
      2. Boots the emulator (or reuses a running one) and waits until Android is ready.
      3. Builds the app in Debug, installs it and starts it on the emulator.

.PARAMETER NoApp
    Only start the emulator, do not build/install/launch the app.

.PARAMETER Release
    Install the Release build instead of Debug.

.PARAMETER Headless
    Run the emulator without a window (used for automated testing).

.PARAMETER Wipe
    Reset the virtual device to factory state before booting (removes all app data).

.EXAMPLE
    .\tools\Start-Emulator.ps1
    Boots the emulator and runs the app.

.EXAMPLE
    .\tools\Start-Emulator.ps1 -NoApp
    Only boots the emulator; deploy later with: dotnet build CasinoTracker\CasinoTracker.csproj -t:Run
#>
[CmdletBinding()]
param(
    [switch] $NoApp,
    [switch] $Release,
    [switch] $Headless,
    [switch] $Wipe
)

$ErrorActionPreference = 'Stop'

# ------------------------------------------------------------------ locations
$RepoRoot   = Split-Path -Parent $PSScriptRoot
$Project    = Join-Path $RepoRoot 'CasinoTracker\CasinoTracker.csproj'
$SdkRoot    = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } elseif ($env:ANDROID_SDK_ROOT) { $env:ANDROID_SDK_ROOT } else { 'C:\Android\android-sdk' }
# The JDK that .NET installed for Android builds is preferred; any JDK 17+ works.
$JavaHome   = if (Test-Path 'C:\Android\jdk\bin\java.exe') { 'C:\Android\jdk' } elseif ($env:JAVA_HOME) { $env:JAVA_HOME } else { throw 'No JDK found. Set JAVA_HOME to a JDK 17 or newer.' }
$Emulator   = Join-Path $SdkRoot 'emulator\emulator.exe'
$Adb        = Join-Path $SdkRoot 'platform-tools\adb.exe'
$AvdManager = Join-Path $SdkRoot 'cmdline-tools\latest\bin\avdmanager.bat'
$SdkManager = Join-Path $SdkRoot 'cmdline-tools\latest\bin\sdkmanager.bat'

$AvdName     = 'CasinoTracker'
$SystemImage = 'system-images;android-34;google_apis;x86_64'
$Device      = 'pixel_6'
$PackageId   = 'com.moritz.casinotracker'

$env:JAVA_HOME = $JavaHome
$env:ANDROID_HOME = $SdkRoot
$env:ANDROID_SDK_ROOT = $SdkRoot

function Write-Step([string] $text) { Write-Host "==> $text" -ForegroundColor Cyan }

# ------------------------------------------------------------------ prerequisites
foreach ($tool in @($Adb, $AvdManager, $SdkManager)) {
    if (-not (Test-Path $tool)) { throw "Missing Android SDK component: $tool. See README.md, section Building." }
}

$SystemImageDir = Join-Path $SdkRoot ($SystemImage -replace ';', '\')
if (-not (Test-Path $Emulator) -or -not (Test-Path $SystemImageDir)) {
    Write-Step "Installing emulator and system image (one-time download, ~1.7 GB)"
    & $SdkManager --sdk_root=$SdkRoot 'emulator' $SystemImage | Where-Object { $_ -notmatch '^\s*\[=*\s*\]\s+\d+%' }
    if ($LASTEXITCODE -ne 0) { throw 'sdkmanager failed.' }
}

# ------------------------------------------------------------------ virtual device
$avdIni = Join-Path $env:USERPROFILE ".android\avd\$AvdName.ini"
if (-not (Test-Path $avdIni)) {
    Write-Step "Creating virtual device '$AvdName' ($Device, Android 14)"
    'no' | & $AvdManager create avd --name $AvdName --package $SystemImage --device $Device --force | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'avdmanager failed.' }

    # A little more memory and a hardware keyboard make testing comfortable.
    $config = Join-Path $env:USERPROFILE ".android\avd\$AvdName.avd\config.ini"
    $lines = Get-Content $config | Where-Object { $_ -notmatch '^(hw\.ramSize|hw\.keyboard|hw\.gpu\.enabled|hw\.gpu\.mode|disk\.dataPartition\.size)=' }
    $lines += 'hw.ramSize=2560'
    $lines += 'hw.keyboard=yes'
    $lines += 'hw.gpu.enabled=yes'
    $lines += 'hw.gpu.mode=auto'
    $lines += 'disk.dataPartition.size=2G'
    $lines | Set-Content $config -Encoding ASCII
}

# ------------------------------------------------------------------ boot
& $Adb start-server | Out-Null
$running = (& $Adb devices) -match '^emulator-\d+\s+device'
if ($running) {
    Write-Step "Emulator already running ($($running[0].Split()[0]))"
} else {
    Write-Step "Booting emulator '$AvdName'"
    $emuArgs = @('-avd', $AvdName, '-netdelay', 'none', '-netspeed', 'full')
    if ($Wipe)     { $emuArgs += '-wipe-data' }
    if ($Headless) { $emuArgs += @('-no-window', '-no-audio', '-no-boot-anim', '-gpu', 'swiftshader_indirect') }
    Start-Process -FilePath $Emulator -ArgumentList $emuArgs | Out-Null

    Write-Host '    waiting for the device ...' -NoNewline
    & $Adb wait-for-device 2>$null
    $deadline = (Get-Date).AddMinutes(4)
    do {
        Start-Sleep -Seconds 2
        Write-Host '.' -NoNewline
        $booted = (& $Adb shell getprop sys.boot_completed 2>$null) -join ''
    } while ($booted.Trim() -ne '1' -and (Get-Date) -lt $deadline)
    Write-Host ''
    if ($booted.Trim() -ne '1') { throw 'The emulator did not finish booting within 4 minutes.' }

    # Unlock the screen in case the lock screen came up.
    & $Adb shell input keyevent 82 2>$null | Out-Null
    Write-Step 'Emulator is ready'
}

if ($NoApp) {
    Write-Host ''
    Write-Host 'Deploy the app any time with:' -ForegroundColor Green
    Write-Host "    dotnet build `"$Project`" -f net10.0-android -t:Run" -ForegroundColor Green
    return
}

# ------------------------------------------------------------------ build, install, launch
$configuration = if ($Release) { 'Release' } else { 'Debug' }
Write-Step "Building ($configuration), installing and launching Casino Tracker"
# The Install/Run targets only exist in the inner (per framework) build, hence -f.
& dotnet build $Project -c $configuration -f net10.0-android -t:Run -v:minimal -nologo
if ($LASTEXITCODE -ne 0) { throw 'Build or deployment failed.' }

Write-Host ''
Write-Host "Casino Tracker is running on the emulator. Logs: adb logcat -s $PackageId" -ForegroundColor Green
