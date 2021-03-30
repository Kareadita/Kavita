﻿# Kavita
![alt text](https://github.com/Kareadita/kareadita.github.io/blob/main/img/features/seriesdetail.PNG?raw=true)

Kavita is a fast, feature rich, cross platform OSS manga server. Built with a focus for manga, 
and the goal of being a full solution for all your reading needs. Setup your own server and share 
your manga collection with your friends and family!

[![Discord](https://img.shields.io/badge/discord-chat-7289DA.svg?maxAge=60)](https://discord.gg/eczRp9eeem)
![Github Downloads](https://img.shields.io/github/downloads/Kareadita/Kavita/total.svg)


## Goals:
* Serve up Manga (cbr, cbz, zip/rar, raw images) and Books (epub, mobi, azw, djvu, pdf)
* Provide Reader for Manga and Books (Light Novels) via web app that is responsive
* Provide customization themes (server installed) for web app
* Provide hooks into metadata providers to fetch Manga data
* Metadata should allow for collections, want to read integration from 3rd party services, genres.
* Ability to manage users, access, and ratings

## How to Build
- Ensure you've cloned Kavita-webui. You should have Projects/Kavita and Projects/Kavita-webui
- In Kavita-webui, run ng serve. This will start the webserver on localhost:4200
- Run API project in Kavita, this will start the backend on localhost:5000


## How to Deploy
- Run build.sh and pass the Runtime Identifier for your OS or just build.sh for all supported RIDs.

## How to install
- Unzip the archive for your target OS
- Place in a directory that is writable. If on windows, do not place in Program Files
- Open appsettings.json and modify TokenKey to a random string ideally generated from [https://passwordsgenerator.net/](https://passwordsgenerator.net/)
- Run Kavita

## Docker
- Docker is supported and tested, you can find the image and instructions [here](https://github.com/Kizaing/KavitaDocker). 

## Buy me a beer
I've gone through many beers building Kavita and expect to go through many more. If you want to throw me a few bucks you can [here](https://paypal.me/majora2007?locale.x=en_US). Money will go 
towards beer or hosting for the upcoming Metadata release. 
