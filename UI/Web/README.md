# Kavita Webui

This project was generated with [Angular CLI](https://github.com/angular/angular-cli) version 11.0.0.

## Development server

Run `npm run start` for a dev server. Navigate to `http://localhost:4200/`. The app will automatically reload if you change any of the source files.
Your backend must be served on port 5000.

## Code scaffolding

Run `ng generate component component-name` to generate a new component. You can also use `ng generate directive|pipe|service|class|guard|interface|enum|module`.

## Build

Run `ng build` to build the project. The build artifacts will be stored in the `dist/` directory. Use the `--prod` flag for a production build.

## Running unit tests

Run `ng test` to execute the unit tests via [Karma](https://karma-runner.github.io).

## Running end-to-end tests

~~Run `ng e2e` to execute the end-to-end tests via [Protractor](http://www.protractortest.org/).~~

Run `npx playwright test --reporter=line` or `npx playwright test` to run e2e tests. 

## Connecting to your dev server via your phone or any other compatible client on local network

Run `npm run start-proxy`

## Testing OIDC

There's two options,

1) Run the proxy and correct the port after redirect (on login).
2) Run `build-backend` or `build-backend-prod`, and use `localhost:5000` to test. This requires you to rebuild after each change

Do **NOT** commit appsettings.development.json while testing OIDC. It'll contain your secret key

## Notes:
- injected services should be at the top of the file
- all components must be standalone

# Update latest angular
`ng update @angular/core @angular/cli @typescript-eslint/parser @angular/localize @angular/compiler-cli @angular/cdk @angular/animations @angular/common @angular/forms @angular/platform-browser @angular/platform-browser-dynamic @angular/router`

`npm install @angular-eslint/builder@latest @angular-eslint/eslint-plugin@latest @angular-eslint/eslint-plugin-template@latest @angular-eslint/schematics@latest @angular-eslint/template-parser@latest`

# Update Localization library
`npm install @jsverse/transloco@latest @jsverse/transloco-locale@latest @jsverse/transloco-persist-lang@latest @jsverse/transloco-persist-translations@latest @jsverse/transloco-preload-langs@latest`
