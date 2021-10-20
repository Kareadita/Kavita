# [<img src="/Logo/kavita.svg" width="32" alt="">]() Kavita
<div align="center">

!![high level view](https://user-images.githubusercontent.com/735851/129777364-2c82d01e-5c03-4daf-b203-92b1d48e5b7b.gif)

Kavita is a fast, feature rich, cross platform reading server. Built with a focus for manga, 
and the goal of being a full solution for all your reading needs. Setup your own server and share 
your reading collection with your friends and family!

[![Release](https://img.shields.io/github/release/Kareadita/Kavita.svg?style=flat&maxAge=3600)](https://github.com/Kareadita/Kavita/releases)
[![License](https://img.shields.io/badge/license-GPLv3-blue.svg?style=flat)](https://github.com/Kareadita/Kavita/blob/master/LICENSE)
[![Downloads](https://img.shields.io/github/downloads/Kareadita/Kavita/total.svg?style=flat)](https://github.com/Kareadita/Kavita/releases)
[![Docker Pulls](https://img.shields.io/docker/pulls/kizaing/kavita.svg)](https://hub.docker.com/r/kizaing/kavita/)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=Kareadita_Kavita&metric=sqale_rating)](https://sonarcloud.io/dashboard?id=Kareadita_Kavita)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=Kareadita_Kavita&metric=security_rating)](https://sonarcloud.io/dashboard?id=Kareadita_Kavita)
[![Backers on Open Collective](https://opencollective.com/kavita/backers/badge.svg)](#backers)
[![Sponsors on Open Collective](https://opencollective.com/kavita/sponsors/badge.svg)](#sponsors)
</div>

## Goals
- [x] Serve up Manga/Webtoons/Comics (cbr, cbz, zip/rar, 7zip, raw images) and Books (epub, pdf)
- [x] First class responsive readers that work great on any device (phone, tablet, desktop)
- [x] Dark and Light themes
- [ ] Provide hooks into metadata providers to fetch metadata for Comics, Manga, and Books
- [ ] Metadata should allow for collections, want to read integration from 3rd party services, genres.
- [x] Ability to manage users, access, and ratings
- [ ] Ability to sync ratings and reviews to external services
- [x] Fully Accessible with active accessibility audits
- [x] Dedicated webtoon reading mode
- [ ] And so much [more...](https://github.com/Kareadita/Kavita/projects)

## Support
[![Reddit](https://img.shields.io/badge/reddit-discussion-FF4500.svg?maxAge=60)](https://www.reddit.com/r/KavitaManga/)
[![Discord](https://img.shields.io/badge/discord-chat-7289DA.svg?maxAge=60)](https://discord.gg/eczRp9eeem)
[![GitHub - Bugs and Feature Requests Only](https://img.shields.io/badge/github-issues-red.svg?maxAge=60)](https://github.com/Kareadita/Kavita/issues)

## Demo
If you want to try out Kavita, we have a demo up:
[https://demo.kavitareader.com/](https://demo.kavitareader.com/)
```
Username: demouser
Password: Demouser64
```

## Setup
### Non-Docker
- Unzip the archive for your target OS
- Place in a directory that is writable. If on windows, do not place in Program Files
- Linux users must ensure the directory & kavita.db is writable by Kavita (might require starting server once) 
- Run Kavita
- If you are updating, copy everything over into install location. All Kavita data is stored in config/, so nothing will be overwritten. 
- Open localhost:5000 and setup your account and libraries in the UI.
### Docker
Running your Kavita server in docker is super easy! Barely an inconvenience. You can run it with this command: 

```
docker run --name kavita -p 5000:5000 \
-v /your/manga/directory:/manga \
-v /kavita/data/directory:/kavita/config \
--restart unless-stopped \
-d kizaing/kavita:latest
```

You can also run it via the docker-compose file:

```
version: '3'
services:
    kavita:
        image: kizaing/kavita:latest
        container_name: kavita
        volumes:
            - ./manga:/manga
            - ./config:/kavita/config
        ports:
            - "5000:5000"
        restart: unless-stopped
```

**Note: Kavita is under heavy development and is being updated all the time, so the tag for current builds is `:nightly`. The `:latest` tag will be the latest stable release.**

## Feature Requests
Got a great idea? Throw it up on the FeatHub or vote on another idea. Please check the [Project Board](https://github.com/Kareadita/Kavita/projects) first for a list of planned features.

[![Feature Requests](https://feathub.com/Kareadita/Kavita?format=svg)](https://feathub.com/Kareadita/Kavita)


## Contributors

This project exists thanks to all the people who contribute. [Contribute](CONTRIBUTING.md).
<a href="https://github.com/Kareadita/Kavita/graphs/contributors"><img src="https://opencollective.com/kavita/contributors.svg?width=890&button=false" /></a>


## Donate
If you like Kavita, have gotten good use out of it or feel like you want to say thanks with a few bucks, feel free to donate. Money will go towards 
expenses related to Kavita. Back us through [OpenCollective](https://opencollective.com/Kavita#backer). You can also use [Paypal](https://www.paypal.com/paypalme/majora2007?locale.x=en_US), however your name will not show below.

## Backers

Thank you to all our backers! 🙏 [Become a backer](https://opencollective.com/Kavita#backer)

<img src="https://opencollective.com/Kavita/backers.svg?width=890"></a>

## Sponsors

Support this project by becoming a sponsor. Your logo will show up here with a link to your website. [Become a sponsor](https://opencollective.com/Kavita#sponsor)

<img src="https://opencollective.com/Kavita/sponsors.svg?width=890"></a>

## Mega Sponsors
<img src="https://opencollective.com/Kavita/tiers/mega-sponsor.svg?width=890"></a>

## JetBrains
Thank you to [<img src="/Logo/jetbrains.svg" alt="" width="32"> JetBrains](http://www.jetbrains.com/) for providing us with free licenses to their great tools.

* [<img src="/Logo/rider.svg" alt="" width="32"> Rider](http://www.jetbrains.com/rider/)
* [<img src="/Logo/dottrace.svg" alt="" width="32"> dotTrace](http://www.jetbrains.com/dottrace/)

## Sentry
Thank you to [<img src="/Logo/sentry.svg" alt="" width="64">](https://sentry.io/welcome/) for providing us with free license to their software.

### License

* [GNU GPL v3](http://www.gnu.org/licenses/gpl.html)
* Copyright 2020-2021

