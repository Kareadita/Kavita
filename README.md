# Kavita
<div align="center">
![alt text](https://github.com/Kareadita/kareadita.github.io/blob/main/img/features/seriesdetail.PNG?raw=true)

Kavita is a fast, feature rich, cross platform reading server. Built with a focus for manga, 
and the goal of being a full solution for all your reading needs. Setup your own server and share 
your reading collection with your friends and family!

[![Release](https://img.shields.io/github/release/Kareadita/Kavita.svg?style=flat&maxAge=3600)](https://github.com/Kareadita/Kavita/releases)
[![License](https://img.shields.io/badge/license-GPLv3-blue.svg?style=flat)](https://github.com/Kareadita/Kavita/blob/master/LICENSE)
[![Discord](https://img.shields.io/badge/discord-chat-7289DA.svg?maxAge=60)](https://discord.gg/eczRp9eeem)
[![Downloads](https://img.shields.io/github/downloads/Kareadita/Kavita/total.svg?style=flat)](https://github.com/Kareadita/Kavita/releases)
[![Docker Pulls](https://img.shields.io/docker/pulls/kizaing/kavita.svg)](https://hub.docker.com/r/kizaing/kavita/)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=Kareadita_Kavita&metric=alert_status)](https://sonarcloud.io/dashboard?id=Kareadita_Kavita)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=Kareadita_Kavita&metric=sqale_rating)](https://sonarcloud.io/dashboard?id=Kareadita_Kavita)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=Kareadita_Kavita&metric=security_rating)](https://sonarcloud.io/dashboard?id=Kareadita_Kavita)
[![Donate via Paypal](https://img.shields.io/badge/donate-paypal-blue.svg?style=popout&logo=paypal)](https://paypal.me/majora2007?locale.x=en_US)
</div>

## Goals:
* Serve up Manga/Webtoons/Comics (cbr, cbz, zip/rar, raw images) and Books (epub, mobi, azw, djvu, pdf)
* First class responsive readers that work great on any device
* Provide a dark theme for web app
* Provide hooks into metadata providers to fetch metadata for Comics, Manga, and Books
* Metadata should allow for collections, want to read integration from 3rd party services, genres.
* Ability to manage users, access, and ratings
* Ability to sync ratings and reviews to external services
* And so much [more...](https://github.com/Kareadita/Kavita/projects)


# How to contribute
- Ensure you've cloned Kavita-webui. You should have Projects/Kavita and Projects/Kavita-webui
- In Kavita-webui, run ng serve. This will start the webserver on localhost:4200
- Run API project in Kavita, this will start the backend on localhost:5000


## Deploy local build
- Run build.sh and pass the Runtime Identifier for your OS or just build.sh for all supported RIDs.

## How to install
- Unzip the archive for your target OS
- Place in a directory that is writable. If on windows, do not place in Program Files
- Run Kavita
- If you are updating, do not copy appsettings.json from the new version over. It will override your TokenKey and you will have to reauthenticate on your devices.

## Docker
Running your Kavita server in docker is super easy! Barely an inconvenience. You can run it with this command: 

```
docker run --name kavita -p 5000:5000 \
-v /your/manga/directory:/manga \
-v /kavita/data/directory:/kavita/data \
--restart unless-stopped \
-d kizaing/kavita:latest
```

You can also run it via the docker-compose file:

```
version: '3.9'
services:
    kavita:
        image: kizaing/kavita:latest
        volumes:
            - ./manga:/manga
            - ./data:/kavita/data
        ports:
            - "5000:5000"
        restart: unless-stopped
```

Note: Kavita is under heavy development and is being updated all the time, so the tag for current builds is :nightly. The :latest tag will be the latest stable release. There is also the :alpine tag if you want a smaller image, but it is only available for x64 systems.

## Got an Idea?
Got a great idea? Throw it up on the FeatHub or vote on another persons. Please check the [Project Board](https://github.com/Kareadita/Kavita/projects) first for a list of planned features.
[![Feature Requests](https://feathub.com/Kareadita/Kavita?format=svg)](https://feathub.com/Kareadita/Kavita)

## Want to help?
I am looking for developers with a passion for building the next Plex for Reading. Developers with C#/ASP.NET, Angular 11 please reach out on [Discord](https://discord.gg/eczRp9eeem).  

## Donate
If you like Kavita, have gotten good use out of it or feel like you want to say thanks with a few bucks, feel free to donate. Money will 
likely go towards beer or hosting.
[![Donate via Paypal](https://img.shields.io/badge/donate-paypal-blue.svg?style=popout&logo=paypal)](https://paypal.me/majora2007?locale.x=en_US)
