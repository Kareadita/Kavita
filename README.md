# Kavita
![alt text](https://github.com/Kareadita/kareadita.github.io/blob/main/img/features/seriesdetail.PNG?raw=true)

Kavita is a fast, feature rich, cross platform OSS manga server. Built with a focus for manga, 
and the goal of being a full solution for all your reading needs. Setup your own server and share 
your manga collection with your friends and family!

[![Discord](https://img.shields.io/badge/discord-chat-7289DA.svg?maxAge=60)](https://discord.gg/eczRp9eeem)
![Github Downloads](https://img.shields.io/github/downloads/Kareadita/Kavita/total.svg)
[![Feature Requests](https://feathub.com/Kareadita/Kavita?format=svg)](https://feathub.com/Kareadita/Kavita)


## Goals:
* Serve up Manga/Webtoons/Comics (cbr, cbz, zip/rar, raw images) and Books (epub, mobi, azw, djvu, pdf)
* Provide Readers via web app that is responsive
* Provide a dark theme for web app
* Provide hooks into metadata providers to fetch Manga data
* Metadata should allow for collections, want to read integration from 3rd party services, genres.
* Ability to manage users, access, and ratings
* Ability to sync ratings and reviews to external services

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
- If you are updating, do not copy appsettings.json from the new version over. It will override your TokenKey and you will have to reauthenticate on your devices.

## Docker
- Docker is supported and tested, you can find the image and instructions [here](https://github.com/Kizaing/KavitaDocker). 

## Want to help?
I am looking for developers with a passion for building the next Plex for Manga, Comics, and Ebooks. I need developers with C#/ASP.NET, Angular 11 or CSS experience. 
Reach out to me on [Discord]((https://discord.gg/eczRp9eeem)).  

## Buy me a beer
I've gone through many beers building Kavita and expect to go through many more. If you want to throw me a few bucks you can [here](https://paypal.me/majora2007?locale.x=en_US). Money will go 
towards beer or hosting for the upcoming Metadata release. 
