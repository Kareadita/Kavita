import { Routes } from '@angular/router';
import { AuthGuard } from '../_guards/auth.guard';
import { LibraryAccessGuard } from '../_guards/library-access.guard';
import { LibraryDetailComponent } from '../library-detail/library-detail.component';
import {PersonDetailComponent} from "../person-detail/person-detail.component";


export const routes: Routes = [
  {
    path: ':personId',
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
