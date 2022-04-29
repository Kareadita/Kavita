import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { AuthGuard } from '../_guards/auth.guard';
import { LibraryAccessGuard } from '../_guards/library-access.guard';
import { LibraryDetailComponent } from './library-detail.component';


const routes: Routes = [
  {
    path: ':libraryId', 
    runGuardsAndResolvers: 'always',
    canActivate: [AuthGuard, LibraryAccessGuard],
    component: LibraryDetailComponent
  },
  {
    path: '', 
    component: LibraryDetailComponent
  }
];


@NgModule({
  imports: [RouterModule.forChild(routes), ],
  exports: [RouterModule]
})
export class LibraryDetailRoutingModule { }
