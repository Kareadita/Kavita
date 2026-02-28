param(
    [string]$RID
)

# stop on first error
$ErrorActionPreference = 'Stop'

$outputFolder = '_output'

function Check-Requirements {
    if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
        Write-Warning 'Warning!!! npm not found, it is required for building Kavita!'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Warning 'Warning!!! dotnet not found, it is required for building Kavita!'
    }
}

function ProgressStart {
    param([string]$Message)
    Write-Host "Start '$Message'"
}

function ProgressEnd {
    param([string]$Message)
    Write-Host "Finish '$Message'"
}

function Build {
    ProgressStart 'Build'

    if (Test-Path $outputFolder) {
        Remove-Item $outputFolder -Recurse -Force
    }

    $slnFile = 'Kavita.sln'

    dotnet clean $slnFile -c Release

    if ([string]::IsNullOrEmpty($RID)) {
        dotnet msbuild -restore $slnFile -p:Configuration=Release -p:Platform='Any CPU'
    }
    else {
        dotnet msbuild -restore $slnFile -p:Configuration=Release -p:Platform='Any CPU' -p:RuntimeIdentifiers=$RID
    }

    ProgressEnd 'Build'
}

function BuildUI {
    ProgressStart 'Building UI'

    Write-Host 'Removing old wwwroot'
    Remove-Item API/wwwroot/* -Recurse -Force -ErrorAction SilentlyContinue

    Push-Location UI/Web
    Write-Host 'Installing web dependencies'
    npm install --legacy-peer-deps
    Write-Host 'Building UI'
    npm run prod
    Write-Host 'Copying back to Kavita wwwroot'
    New-Item -ItemType Directory -Path '../../API/wwwroot' -Force | Out-Null
    Copy-Item -Path 'dist/browser/*' -Destination '../../API/wwwroot' -Recurse -Force
    Pop-Location

    ProgressEnd 'Building UI'
}

function Package {
    param([string]$runtime)

    # Compute the path from the current repo root before any directory changes
    $repoRoot = Get-Location
    $lOutputFolder = Join-Path $repoRoot "_output" $runtime "Kavita"

    ProgressStart "Creating $runtime Package"

    Write-Host 'Building'
    Push-Location API
    dotnet publish -c Release --self-contained --runtime $runtime -o "$lOutputFolder"

    Write-Host 'Recopying wwwroot due to bug'
    Copy-Item -Path './wwwroot/*' -Destination "$lOutputFolder/wwwroot" -Recurse -Force

    Write-Host 'Removing EF Core design-time folders'
    Remove-Item "$lOutputFolder/BuildHost-net472" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item "$lOutputFolder/BuildHost-netcore" -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host 'Removing cache-long from config'
    Remove-Item "$lOutputFolder/config/cache-long" -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host 'Copying Install information'
    Copy-Item '..\INSTALL.txt' "$lOutputFolder/README.txt" -Force

    Write-Host 'Copying LICENSE'
    Copy-Item '..\LICENSE' "$lOutputFolder/LICENSE.txt" -Force

    Write-Host 'Renaming API -> Kavita'
    if ($runtime -in @('win-x64','win-x86')) {
        Move-Item "$lOutputFolder/API.exe" "$lOutputFolder/Kavita.exe" -Force
    }
    else {
        Move-Item "$lOutputFolder/API" "$lOutputFolder/Kavita" -Force
    }

    Write-Host 'Copying appsettings.json'
    Copy-Item 'config/appsettings.json' "$lOutputFolder/config/appsettings-init.json" -Force
    Write-Host 'Removing appsettings.Development.json'
    Remove-Item "$lOutputFolder/config/appsettings.Development.json" -Force -ErrorAction SilentlyContinue
    Write-Host 'Removing appsettings.json'
    Remove-Item "$lOutputFolder/config/appsettings.json" -Force -ErrorAction SilentlyContinue

    Write-Host 'Creating tar'
    # switch back to repository root (pop off the API location)
    Pop-Location

    # move into the specific runtime output directory before tarring
    Push-Location $lOutputFolder
    if (Test-Path "../kavita-$runtime.tar.gz") { Remove-Item "../kavita-$runtime.tar.gz" -Force }
    # tar should be available on Windows 10+; use native if not.  run from inside the runtime folder
    tar -czvf "../kavita-$runtime.tar.gz" "Kavita"
    Pop-Location

    ProgressEnd "Creating $runtime Package"
}

# script entry

Check-Requirements
BuildUI
Build

$dir = Get-Location

if ([string]::IsNullOrEmpty($RID)) {
    Package 'win-x64'
    Set-Location $dir
    Package 'win-x86'
    Set-Location $dir
    Package 'linux-x64'
    Set-Location $dir
    Package 'linux-arm'
    Set-Location $dir
    Package 'linux-arm64'
    Set-Location $dir
    Package 'linux-musl-x64'
    Set-Location $dir
    Package 'osx-x64'
    Set-Location $dir
    Package 'osx-arm64'
    Set-Location $dir
}
else {
    Package $RID
    Set-Location $dir
}
