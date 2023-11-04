import { Routes } from '@angular/router';
import { AuthGuard } from '../_guards/auth.guard';
import { LibraryAccessGuard } from '../_guards/library-access.guard';
import { LibraryDetailComponent } from '../library-detail/library-detail.component';


export const routes: Routes = [
  {
    path: ':libraryId',
    runGuardsAndResolvers: 'always',
    canActivate: [AuthGuard, LibraryAccessGuard],
    component: LibraryDetailComponent
  },
  {
    path: '',
    runGuardsAndResolvers: 'always',
    canActivate: [AuthGuard, LibraryAccessGuard],
    component: LibraryDetailComponent
  }
];
