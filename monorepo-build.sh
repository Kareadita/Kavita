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
    local runtime="$1"
    local lOutputFolder=../_output/"$runtime"/Kavita

    ProgressStart "Creating $runtime Package"

    # TODO: Use no-restore? Because Build should have already done it for us
    echo "Building"
    cd API
    echo dotnet publish -c Release --no-restore --self-contained --runtime $runtime -o "$lOutputFolder"
    dotnet publish -c Release --no-restore --self-contained --runtime $runtime -o "$lOutputFolder"

    echo "Renaming API -> Kavita"
    mv "$lOutputFolder"/API "$lOutputFolder"/Kavita

    echo "Copying webui wwwroot to build"
    cp -r wwwroot/* "$lOutputFolder"/wwwroot/

    echo "Copying Install information"
    cp ../INSTALL.txt "$lOutputFolder"/README.txt

    echo "Copying LICENSE"
    cp ../LICENSE "$lOutputFolder"/LICENSE.txt

	echo "Copying appsettings.json"
    cp ./config/appsettings.Development.json $lOutputFolder/config/appsettings.json

    echo "Creating tar"
    cd ../$outputFolder/"$runtime"/
    tar -czvf ../kavita-$runtime.tar.gz Kavita

    ProgressEnd "Creating $runtime Package"

}

dir=$PWD

if [ -d _output ]
then
	rm -r _output/
fi

#Build for x64
Build "linux-x64"
Package "linux-x64"
cd "$dir"

#Build for arm
Build "linux-arm"
Package "linux-arm"
cd "$dir"

#Build for arm64
Build "linux-arm64"
Package "linux-arm64"
cd "$dir"
