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

UpdateVersionNumber()
{
  # TODO: Enhance this to increment version number in KavitaCommon.csproj
    if [ "$KAVITAVERSION" != "" ]; then
        echo "Updating Version Info"
        sed -i'' -e "s/<AssemblyVersion>[0-9.*]\+<\/AssemblyVersion>/<AssemblyVersion>$KAVITAVERSION<\/AssemblyVersion>/g" src/Directory.Build.props
        sed -i'' -e "s/<AssemblyConfiguration>[\$()A-Za-z-]\+<\/AssemblyConfiguration>/<AssemblyConfiguration>${BUILD_SOURCEBRANCHNAME}<\/AssemblyConfiguration>/g" src/Directory.Build.props
#        sed -i'' -e "s/<string>10.0.0.0<\/string>/<string>$KAVITAVERSION<\/string>/g" macOS/Kavita.app/Contents/Info.plist
    fi
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
    npm install
    echo 'Building UI'
    npm run prod
    echo 'Copying back to Kavita wwwroot'
    mkdir -p ../../API/wwwroot
    cp -R dist/* ../../API/wwwroot
    cd ../../ || exit
    ProgressEnd 'Building UI'
}

Package()
{
    local framework="$1"
    local runtime="$2"
    local lOutputFolder=../_output/"$runtime"/Kavita

    ProgressStart "Creating $runtime Package for $framework"

    # TODO: Use no-restore? Because Build should have already done it for us
    echo "Building"
    cd API
    echo dotnet publish -c Release --self-contained --runtime $runtime -o "$lOutputFolder" --framework $framework
    dotnet publish -c Release --self-contained --runtime $runtime -o "$lOutputFolder" --framework $framework

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
    cp config/appsettings.Development.json $lOutputFolder/config/appsettings.json

    echo "Creating tar"
    cd ../$outputFolder/"$runtime"/
    tar -czvf ../kavita-$runtime.tar.gz Kavita


    ProgressEnd "Creating $runtime Package for $framework"


}


#UpdateVersionNumber

RID="$1"

CheckRequirements
BuildUI
Build

dir=$PWD

if [[ -z "$RID" ]];
then
    Package "net5.0" "win-x64"
    cd "$dir"
    Package "net5.0" "win-x86"
    cd "$dir"
    Package "net5.0" "linux-x64"
    cd "$dir"
    Package "net5.0" "linux-arm"
    cd "$dir"
    Package "net5.0" "linux-arm64"
    cd "$dir"
    Package "net5.0" "linux-musl-x64"
    cd "$dir"
    Package "net5.0" "osx-x64"
    cd "$dir"
else
    Package "net5.0" "$RID"
    cd "$dir"
fi
