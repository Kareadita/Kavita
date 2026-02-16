# PowerShell build script for Kavita on Windows
param(
    [string]$RID = ""
)

$ErrorActionPreference = "Stop"
$outputFolder = "_output"

function CheckRequirements {
    if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
        Write-Warning "npm not found, it is required for building Kavita!"
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Warning "dotnet not found, it is required for building Kavita!"
    }
}

function ProgressStart {
    param([string]$task)
    Write-Host "Start '$task'" -ForegroundColor Cyan
}

function ProgressEnd {
    param([string]$task)
    Write-Host "Finish '$task'" -ForegroundColor Green
}

function Build {
    ProgressStart "Build"

    if (Test-Path $outputFolder) {
        Remove-Item -Path $outputFolder -Recurse -Force
    }

    $slnFile = "Kavita.sln"

    dotnet clean $slnFile -c Release

    if ([string]::IsNullOrEmpty($RID)) {
        dotnet msbuild -restore $slnFile -p:Configuration=Release -p:Platform="Any CPU"
    }
    else {
        dotnet msbuild -restore $slnFile -p:Configuration=Release -p:Platform="Any CPU" -p:RuntimeIdentifiers=$RID
    }

    ProgressEnd "Build"
}

function BuildUI {
    ProgressStart "Building UI"
    
    Write-Host "Removing old wwwroot"
    if (Test-Path "API/wwwroot") {
        Remove-Item -Path "API/wwwroot/*" -Recurse -Force -ErrorAction SilentlyContinue
    }
    
    Push-Location "UI/Web"
    try {
        Write-Host "Installing web dependencies"
        npm install --legacy-peer-deps
        
        Write-Host "Building UI"
        npm run prod
        
        Write-Host "Copying back to Kavita wwwroot"
        $wwwrootPath = "../../API/wwwroot"
        if (-not (Test-Path $wwwrootPath)) {
            New-Item -Path $wwwrootPath -ItemType Directory -Force | Out-Null
        }
        Copy-Item -Path "dist/browser/*" -Destination $wwwrootPath -Recurse -Force
    }
    finally {
        Pop-Location
    }
    
    ProgressEnd "Building UI"
}

function Package {
    param([string]$runtime)
    
    $lOutputFolder = "../$outputFolder/$runtime/Kavita"
    
    ProgressStart "Creating $runtime Package"
    
    Push-Location "API"
    try {
        Write-Host "Building"
        Write-Host "dotnet publish -c Release --self-contained --runtime $runtime -o `"$lOutputFolder`""
        dotnet publish -c Release --self-contained --runtime $runtime -o "$lOutputFolder"
        
        Write-Host "Recopying wwwroot due to bug"
        Copy-Item -Path "./wwwroot/*" -Destination "$lOutputFolder/wwwroot" -Recurse -Force
        
        Write-Host "Removing EF Core design-time folders"
        if (Test-Path "$lOutputFolder/BuildHost-net472") {
            Remove-Item -Path "$lOutputFolder/BuildHost-net472" -Recurse -Force
        }
        if (Test-Path "$lOutputFolder/BuildHost-netcore") {
            Remove-Item -Path "$lOutputFolder/BuildHost-netcore" -Recurse -Force
        }
        
        Write-Host "Removing cache-long from config"
        if (Test-Path "$lOutputFolder/config/cache-long") {
            Remove-Item -Path "$lOutputFolder/config/cache-long" -Recurse -Force
        }
        
        Write-Host "Copying Install information"
        Copy-Item -Path "../INSTALL.txt" -Destination "$lOutputFolder/README.txt" -Force
        
        Write-Host "Copying LICENSE"
        Copy-Item -Path "../LICENSE" -Destination "$lOutputFolder/LICENSE.txt" -Force
        
        Write-Host "Renaming API -> Kavita"
        if ($runtime -eq "win-x64" -or $runtime -eq "win-x86") {
            if (Test-Path "$lOutputFolder/API.exe") {
                Rename-Item -Path "$lOutputFolder/API.exe" -NewName "Kavita.exe" -Force
            }
        }
        else {
            if (Test-Path "$lOutputFolder/API") {
                Rename-Item -Path "$lOutputFolder/API" -NewName "Kavita" -Force
            }
        }
        
        Write-Host "Copying appsettings.json"
        Copy-Item -Path "config/appsettings.json" -Destination "$lOutputFolder/config/appsettings-init.json" -Force
        
        Write-Host "Removing appsettings.Development.json"
        if (Test-Path "$lOutputFolder/config/appsettings.Development.json") {
            Remove-Item -Path "$lOutputFolder/config/appsettings.Development.json" -Force
        }
        
        Write-Host "Removing appsettings.json"
        if (Test-Path "$lOutputFolder/config/appsettings.json") {
            Remove-Item -Path "$lOutputFolder/config/appsettings.json" -Force
        }
        
        Write-Host "Creating zip archive"
        Push-Location "../$outputFolder/$runtime"
        try {
            $archivePath = "../kavita-$runtime.zip"
            if (Test-Path $archivePath) {
                Remove-Item -Path $archivePath -Force
            }
            Compress-Archive -Path "Kavita" -DestinationPath $archivePath -Force
        }
        finally {
            Pop-Location
        }
    }
    finally {
        Pop-Location
    }
    
    ProgressEnd "Creating $runtime Package"
}

# Main execution
$scriptDir = $PSScriptRoot
Set-Location $scriptDir

CheckRequirements
BuildUI
Build

if ([string]::IsNullOrEmpty($RID)) {
    Package "win-x64"
    Set-Location $scriptDir
    Package "win-x86"
    Set-Location $scriptDir
    Package "linux-x64"
    Set-Location $scriptDir
    Package "linux-arm"
    Set-Location $scriptDir
    Package "linux-arm64"
    Set-Location $scriptDir
    Package "linux-musl-x64"
    Set-Location $scriptDir
    Package "osx-x64"
    Set-Location $scriptDir
    Package "osx-arm64"
    Set-Location $scriptDir
}
else {
    Package $RID
    Set-Location $scriptDir
}

Write-Host "Build completed successfully!" -ForegroundColor Green
