# Kavita
Kavita, meaning Stories, is a lightweight manga server. The goal is to replace Ubooqity with an 
open source variant that is flexible and packs more punch, without sacrificing ease to use. 

## Goals:
* Serve up Manga (cbr, cbz, zip/rar, raw images) and Books (epub, mobi, azw, djvu, pdf)
* Provide Reader for Manga and Books (Light Novels) via web app that is responsive
* Provide customization themes (server installed) for web app
* Provide hooks into metadata providers to fetch Manga data
* Metadata should allow for collections, want to read integration from 3rd party services, genres.
* Ability to manage users, access, and ratings
* Expose an OPDS API/Stream for external readers to use
* Allow downloading files directly from WebApp


## How to Deploy
* Build kavita-webui via ng build --prod. The dest should be placed in the API/wwwroot directory
* Run publish command
