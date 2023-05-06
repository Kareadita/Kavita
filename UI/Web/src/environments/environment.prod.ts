import { getBaseUrl } from "src/app/base-url.provider";
const BASE_URL = getBaseUrl();

export const environment = {
  production: true,
  apiUrl: `${BASE_URL}api/`,
  hubUrl:`${BASE_URL}hubs/`
};
