import { getBaseUrl } from "src/app/base-url.provider";
const BASE_URL = getBaseUrl();

export const environment = {
  production: true,
  apiUrl: `${BASE_URL}api/`,
  hubUrl:`${BASE_URL}hubs/`,
  buyLink: 'https://buy.stripe.com/3cs7uw67p2Re7JK4gj?prefilled_promo_code=FREETRIAL',
  manageLink: 'https://billing.stripe.com/p/login/28oaFRa3HdHWb5ecMM'
};
