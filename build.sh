#! /bin/bash
set -e

outputFolder='_output'

CheckRequirements()
{
    if ! command -v npm &> /dev/null
    then
        echo "Warning!!! npm not found, it is required for building Kavita!"
    fi
    if ! command -v dotnet &> /dev/null
    then
        echo "Warning!!! dotnet not found, it is required for building Kavita!"
    fi
}

ProgressStart()
{
    echo "Start '$1'"
}

ProgressEnd()
{
    echo "Finish '$1'"
}


Build()
{
    ProgressStart 'Build'

    rm -rf $outputFolder

    slnFile=Kavita.sln

    dotnet clean $slnFile -c Release

    if [[ -z "$RID" ]];
    then
        dotnet msbuild -restore $slnFile -p:Configuration=Release -p:Platform="Any CPU"
    else
        dotnet msbuild -restore $slnFile -p:Configuration=Release -p:Platform="Any CPU" -p:RuntimeIdentifiers=$RID
    fi

    ProgressEnd 'Build'
}

BuildUI()
{
    ProgressStart 'Building UI'
    echo 'Removing old wwwroot'
    rm -rf API/wwwroot/*
    cd UI/Web/ || exit
    echo 'Installing web dependencies'
    npm install --legacy-peer-deps
    echo 'Building UI'
    npm run prod
    echo 'Copying back to Kavita wwwroot'
    mkdir -p ../../API/wwwroot
    cp -R dist/browser/* ../../API/wwwroot
    cd ../../ || exit
    ProgressEnd 'Building UI'
}

Package()
{
    local runtime="$1"
    local lOutputFolder=../_output/"$runtime"/Kavita

    ProgressStart "Creating $runtime Package"

    # TODO: Use no-restore? Because Build should have already done it for us
    echo "Building"
    cd API
    echo dotnet publish -c Release --self-contained --runtime $runtime -o "$lOutputFolder"
    dotnet publish -c Release --self-contained --runtime $runtime -o "$lOutputFolder"

    echo "Recopying wwwroot due to bug"
    cp -R ./wwwroot/* $lOutputFolder/wwwroot

    echo "Copying Install information"
    cp ../INSTALL.txt "$lOutputFolder"/README.txt

    echo "Copying LICENSE"
    cp ../LICENSE "$lOutputFolder"/LICENSE.txt

    echo "Renaming API -> Kavita"
    if [ $runtime == "win-x64" ] || [ $runtime == "win-x86" ]
    then
        mv "$lOutputFolder"/API.exe "$lOutputFolder"/Kavita.exe
    else
        mv "$lOutputFolder"/API "$lOutputFolder"/Kavita
    fi

    echo "Copying appsettings.json"
    cp config/appsettings.json $lOutputFolder/config/appsettings.json
    echo "Removing appsettings.Development.json"
    rm $lOutputFolder/config/appsettings.Development.json

    echo "Creating tar"
    cd ../$outputFolder/"$runtime"/
    tar -czvf ../kavita-$runtime.tar.gz Kavita


    ProgressEnd "Creating $runtime Package"


}


RID="$1"

CheckRequirements
BuildUI
Build

dir=$PWD

if [[ -z "$RID" ]];
then
    Package "win-x64"
    cd "$dir"
    Package "win-x86"
    cd "$dir"
    Package "linux-x64"
    cd "$dir"
    Package "linux-arm"
    cd "$dir"
    Package "linux-arm64"
    cd "$dir"
    Package "linux-musl-x64"
    cd "$dir"
    Package "osx-x64"
    cd "$dir"
    Package "osx-arm64"
    cd "$dir"
else
    Package "$RID"
    cd "$dir"
fi
