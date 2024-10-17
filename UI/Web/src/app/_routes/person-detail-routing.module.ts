import { Routes } from '@angular/router';
import { AuthGuard } from '../_guards/auth.guard';
import {PersonDetailComponent} from "../person-detail/person-detail.component";


export const routes: Routes = [
  {
    path: ':name',
    runGuardsAndResolvers: 'always',
    canActivate: [AuthGuard],
    component: PersonDetailComponent
  },
  {
    path: '',
    runGuardsAndResolvers: 'always',
    canActivate: [AuthGuard],
    component: PersonDetailComponent
  }
];
