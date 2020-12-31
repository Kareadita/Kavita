import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { HomeComponent } from './home/home.component';
import { LibraryDetailComponent } from './library-detail/library-detail.component';
import { LibraryComponent } from './library/library.component';
import { SeriesDetailComponent } from './series-detail/series-detail.component';

const routes: Routes = [
  {path: '', component: HomeComponent},
  {
    path: 'admin',
    loadChildren: () => import('./admin/admin.module').then(m => m.AdminModule)
  },
  {path: 'library', component: LibraryComponent},
  {path: 'library/:id', component: LibraryDetailComponent}, // NOTE: Should I put a guard up to prevent unauthorized access to libraries and series? 
  {path: 'series/:id', component: SeriesDetailComponent},
  {path: '**', component: HomeComponent, pathMatch: 'full'}
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
