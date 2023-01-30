/// <reference types="@angular/localize" />

import { enableProdMode } from '@angular/core';
import { platformBrowserDynamic } from '@angular/platform-browser-dynamic';
import { AppModule } from './app/app.module';
import { environment } from './environments/environment';

if (environment.production) {
  enableProdMode();
}

// function fetchConfig(): Promise<ConfigData> {
//   return fetch(environment.apiUrl + 'settings/base-url')
//     .then(response => response.text())
//     .then(response => new ConfigData(response));
// }

// fetchConfig().then(config => {
  
// });
platformBrowserDynamic().bootstrapModule(AppModule)
    .catch(err => console.error(err));