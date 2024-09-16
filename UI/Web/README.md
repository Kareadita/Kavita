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

Update `IP` constant in `src/environments/environment.ts` to your dev machine's ip instead of `localhost`.

Run `npm run start`

## Notes:
- injected services should be at the top of the file
- all components must be standalone

# Update latest angular
`ng update @angular/core @angular/cli @typescript-eslint/parser @angular/localize @angular/compiler-cli @angular-devkit/build-angular @angular/cdk`
