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

UpdateVersionNumber()
{
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

    dotnet clean $slnFile -c Debug
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
    cd ../kavita-webui/ || exit
    npm install
    npm run prod
    cd ../kavita/ || exit
    ProgressEnd 'Building UI'
}

Package()
{
    local framework="$1"
    local runtime="$2"
    local lOutputFolder=../_output/"$runtime"/Kavita

    ProgressStart "Creating $runtime Package for $framework"

    BuildUI
    
    echo "Building"
    cd API
    echo dotnet publish -c release --self-contained --runtime $runtime -o "$lOutputFolder" --framework $framework
    dotnet publish -c release --self-contained --runtime $runtime -o "$lOutputFolder" --framework $framework

    echo "Copying Install information"
    cp ../INSTALL.txt "$lOutputFolder"/README.txt
    
    echo "Renaming API -> Kavita"
    mv "$lOutputFolder"/API "$lOutputFolder"/Kavita

    echo "Creating tar"
    cd ../$outputFolder/"$runtime"/
    tar -czvf ../kavita-$runtime.tar.gz Kavita
    

    ProgressEnd "Creating $runtime Package for $framework"
    

}


#UpdateVersionNumber

RID="$1"

Build

if [[ -z "$RID" ]];
then
    Package "net5.0" "win-x64"
    Package "net5.0" "win-x86"
    Package "net5.0" "linux-x64"
    Package "net5.0" "linux-musl-x64"
    Package "net5.0" "osx-x64"
else
    Package "net5.0" "$RID"
fi



