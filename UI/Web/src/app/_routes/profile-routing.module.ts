import {Routes} from "@angular/router";
import {ProfileComponent} from "../profile/_components/profile/profile.component";
import {memberInfoResolver} from "../_resolvers/member-info.resolver";
import {profileGuard} from "../_guards/profile.guard";


export const routes: Routes = [
  {
    path: ':userId',
    component: ProfileComponent,
    pathMatch: 'full',
    //canActivate: [profileGuard],
    resolve: {
      memberInfo: memberInfoResolver
    }
  },
];
