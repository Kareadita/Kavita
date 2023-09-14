#! /bin/bash
set -e

outputFolder='_output'

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
	  local RID="$1"

    ProgressStart 'Build for $RID'

    slnFile=Kavita.sln

    dotnet clean $slnFile -c Debug
    dotnet clean $slnFile -c Release

	  dotnet msbuild -restore $slnFile -p:Configuration=Release -p:Platform="Any CPU" -p:RuntimeIdentifiers=$RID

    ProgressEnd 'Build for $RID'
}

BuildUI()
{
    ProgressStart 'Building UI'
    cd ../Kavita-webui/ || exit
    npm install
    npm run prod
    cd ../Kavita/ || exit
    ProgressEnd 'Building UI'

    ProgressStart 'Building UI'
    echo 'Removing old wwwroot'
    rm -rf API/wwwroot/*
    cd ../Kavita-webui/ || exit
    echo 'Installing web dependencies'
    npm install
    echo 'Building UI'
    npm run prod
    echo 'Copying back to Kavita wwwroot'
    cp -r dist/* ../Kavita/API/wwwroot
    cd ../Kavita/ || exit
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
    echo dotnet publish -c Release --no-restore --self-contained --runtime $runtime -o "$lOutputFolder" --framework $framework
    dotnet publish -c Release --no-restore --self-contained --runtime $runtime -o "$lOutputFolder" --framework $framework

    echo "Copying Install information"
    cp ../INSTALL.txt "$lOutputFolder"/README.txt

    echo "Copying LICENSE"
    cp ../LICENSE "$lOutputFolder"/LICENSE.txt

    echo "Renaming API -> Kavita"
    mv "$lOutputFolder"/API "$lOutputFolder"/Kavita

    echo "Creating tar"
    cd ../$outputFolder/"$runtime"/
    tar -czvf ../kavita-$runtime.tar.gz Kavita

    ProgressEnd "Creating $runtime Package for $framework"

}

dir=$PWD

if [ -d _output ]
then
	rm -r _output/
fi

BuildUI

#Build for x64
Build "linux-x64"
Package "net7.0" "linux-x64"
cd "$dir"

#Build for arm
Build "linux-arm"
Package "net7.0" "linux-arm"
cd "$dir"

#Build for arm64
Build "linux-arm64"
Package "net7.0" "linux-arm64"
cd "$dir"

#Builds Docker images
docker buildx build -t kizaing/kavita:nightly --platform linux/amd64,linux/arm/v7,linux/arm64 . --push
