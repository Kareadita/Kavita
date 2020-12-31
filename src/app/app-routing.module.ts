import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { HomeComponent } from './home/home.component';
import { LibraryDetailComponent } from './library-detail/library-detail.component';
import { LibraryComponent } from './library/library.component';

const routes: Routes = [
  {path: '', component: HomeComponent},
  {
    path: 'admin',
    loadChildren: () => import('./admin/admin.module').then(m => m.AdminModule)
  },
  {path: 'library', component: LibraryComponent},
  {path: 'library/:id', component: LibraryDetailComponent},
  {path: '**', component: HomeComponent, pathMatch: 'full'}
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
