# Kavita
Kavita, meaning Stories, is a lightweight manga server. The goal is to replace Ubooquity with an 
open source variant that is flexible and packs more punch, without sacrificing ease to use. 
Think: ***Plex but for Manga.***

## Goals:
* Serve up Manga (cbr, cbz, zip/rar, raw images) and Books (epub, mobi, azw, djvu, pdf)
* Provide Reader for Manga and Books (Light Novels) via web app that is responsive
* Provide customization themes (server installed) for web app
* Provide hooks into metadata providers to fetch Manga data
* Metadata should allow for collections, want to read integration from 3rd party services, genres.
* Ability to manage users, access, and ratings


## How to Deploy
- Run build.sh and pass the Runtime Identifier for your OS or just build.sh for all supported RIDs.

## How to install
1. Unzip the archive for your target OS
2. Place in a directory that is writable. If on windows, do not place in Program Files
3. Open appsettings.json and modify TokenKey to a random string ideally generated from [https://passwordsgenerator.net/](https://passwordsgenerator.net/)
4. Run Kavita

