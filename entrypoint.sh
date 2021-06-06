#!/bin/bash

#Checks if a token has been set, and then generates a new token if not
if grep -q 'super secret unguessable key' /kavita/appsettings.json
then
	export TOKEN_KEY="$(pwgen -s 16 1)"
	sed -i "s/super secret unguessable key/${TOKEN_KEY}/g" /kavita/appsettings.json
fi

#Checks if the appsettings.json already exists in bind mount
if test -f "/kavita/data/appsettings.json"
then
	rm /kavita/appsettings.json
	ln -s /kavita/data/appsettings.json /kavita/
else
	mv /kavita/appsettings.json /kavita/data/
	ln -s /kavita/data/appsettings.json /kavita/
fi

#Checks if the data folders exist
if [ -d /kavita/data/temp ]
then
	if [ -d /kavita/temp ]
	then
		unlink /kavita/temp
		ln -s /kavita/data/temp /kavita/temp
	else
		ln -s /kavita/data/temp /kavita/temp
	fi
else
	mkdir /kavita/data/temp
	ln -s /kavita/data/temp /kavita/temp
fi

if [ -d /kavita/data/cache ]
then
	if [ -d /kavita/cache ]
	then
		unlink /kavita/cache
		ln -s /kavita/data/cache /kavita/cache
	else
		ln -s /kavita/data/cache /kavita/cache
	fi
else
	mkdir /kavita/data/cache
	ln -s /kavita/data/cache /kavita/cache
fi

# Checks for the log file

if test -f "/kavita/data/logs/kavita.log"
then
	rm /kavita/kavita.log
	ln -s /kavita/data/logs/kavita.log /kavita/
else
	if [ -d /kavita/data/logs ]
	then
		touch /kavita/data/logs/kavita.log
		ln -s /kavita/data/logs/kavita.log /kavita/
	else
		mkdir /kavita/data/logs
		touch /kavita/data/logs/kavita.log
		ln -s /kavita/data/logs/kavita.log /kavita/
	fi

fi

./Kavita
