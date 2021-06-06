#!/bin/bash

mkdir Projects

cd Projects

git clone https://github.com/Kareadita/Kavita.git
git clone https://github.com/Kareadita/Kavita-webui.git

cd Kavita
chmod +x build.sh

#Builds program based on the target platform

if [ "$TARGETPLATFORM" == "linux/amd64" ]
then
	./build.sh linux-x64
	mv /Projects/Kavita/_output/linux-x64 /Projects/Kavita/_output/build
elif [ "$TARGETPLATFORM" == "linux/arm/v7" ]
then
	./build.sh linux-arm
	mv /Projects/Kavita/_output/linux-arm /Projects/Kavita/_output/build
elif [ "$TARGETPLATFORM" == "linux/arm64" ]
then
	./build.sh linux-arm64
	mv /Projects/Kavita/_output/linux-arm64 /Projects/Kavita/_output/build
fi
