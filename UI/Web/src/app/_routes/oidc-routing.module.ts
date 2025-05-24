import {Routes} from "@angular/router";
import {OidcCallbackComponent} from "../registration/oidc-callback/oidc-callback.component";

export const routes: Routes = [
  {
    path: 'callback',
    component: OidcCallbackComponent,
  }
];
