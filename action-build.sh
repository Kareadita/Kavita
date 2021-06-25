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

    ProgressStart "Build for $RID"

    slnFile=Kavita.sln

    dotnet clean $slnFile -c Release

	dotnet msbuild -restore $slnFile -p:Configuration=Release -p:Platform="Any CPU" -p:RuntimeIdentifiers=$RID

    ProgressEnd "Build for $RID"
}

Package()
{
    local framework="$1"
    local runtime="$2"
    local lOutputFolder=../_output/"$runtime"/Kavita

    echo "Integrity check on root folder"
    ls -l

    ProgressStart "Creating $runtime Package for $framework"

    # TODO: Use no-restore? Because Build should have already done it for us
    echo "Building"
    cd API
    echo dotnet publish -c Release --no-restore --self-contained --runtime $runtime -o "$lOutputFolder" --framework $framework
    dotnet publish -c Release --no-restore --self-contained --runtime $runtime -o "$lOutputFolder" --framework $framework

    echo "Integrity check on API wwwroot folder"
    ls -l "$lOutputFolder"/wwwroot

    echo "Renaming API -> Kavita"
    mv "$lOutputFolder"/API "$lOutputFolder"/Kavita

    echo "Integrity check on Kavita wwwroot folder"
    ls -l "$lOutputFolder"/wwwroot

    echo "Copying Install information"
    cp ../INSTALL.txt "$lOutputFolder"/README.txt
    
    echo "Copying LICENSE"
    cp ../LICENSE "$lOutputFolder"/LICENSE.txt

    echo "Creating tar"
    cd ../$outputFolder/"$runtime"/
    tar -czvf ../kavita-$runtime.tar.gz Kavita
    
    ProgressEnd "Creating $runtime Package for $framework"    

}

BuildUI()
{
    ProgressStart 'Building UI'
    echo 'Removing old wwwroot'
    rm -rf API/wwwroot/*
    cd ../Kavita-webui/ || exit
    echo 'Installing web dependencies'
    npm install
    echo 'Building UI'
    npm run prod
    ls -l dist
    echo 'Copying back to Kavita wwwroot'
    cp -r dist/* ../Kavita/API/wwwroot
    ls -l ../Kavita/API/wwwroot
    cd ../Kavita/ || exit
    ProgressEnd 'Building UI'
}

dir=$PWD

if [ -d _output ]
then
	rm -r _output/
fi

#Build for x64
Build "linux-x64"
Package "net5.0" "linux-x64"
cd "$dir"

#Build for arm
Build "linux-arm"
Package "net5.0" "linux-arm"
cd "$dir"

#Build for arm64
Build "linux-arm64"
Package "net5.0" "linux-arm64"
cd "$dir"