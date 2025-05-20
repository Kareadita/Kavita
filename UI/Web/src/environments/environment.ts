// This file can be replaced during build by using the `fileReplacements` array.
// `ng build --prod` replaces `environment.ts` with `environment.prod.ts`.
// The list of file replacements can be found in `angular.json`.

const IP = 'localhost';

export const environment = {
  production: false,
  apiUrl: 'http://' + IP + ':5000/api/',
  hubUrl: 'http://'+ IP + ':5000/hubs/',
  buyLink: 'https://buy.stripe.com/test_9AQ5mi058h1PcIo3cf?prefilled_promo_code=FREETRIAL',
  manageLink: 'https://billing.stripe.com/p/login/test_14kfZocuh6Tz5ag7ss'
};

/*
 * For easier debugging in development mode, you can import the following file
 * to ignore zone related error stack frames such as `zone.run`, `zoneDelegate.invokeTask`.
 *
 * This import should be commented out in production mode because it will have a negative impact
 * on performance if an error is thrown.
 */
// import 'zone.js/plugins/zone-error';  // Included with Angular CLI.
