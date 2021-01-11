import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { HomeComponent } from './home/home.component';
import { LibraryDetailComponent } from './library-detail/library-detail.component';
import { LibraryComponent } from './library/library.component';
import { MangaReaderComponent } from './manga-reader/manga-reader.component';
import { SeriesDetailComponent } from './series-detail/series-detail.component';
import { UserLoginComponent } from './user-login/user-login.component';
import { AuthGuard } from './_guards/auth.guard';
import { LibraryAccessGuard } from './_guards/library-access.guard';


const routes: Routes = [
  {path: '', component: HomeComponent},
  {
    path: 'admin',
    loadChildren: () => import('./admin/admin.module').then(m => m.AdminModule)
  },
  {path: 'library', component: LibraryComponent},
  {
    path: '',
    runGuardsAndResolvers: 'always',
    canActivate: [AuthGuard, LibraryAccessGuard],
    children: [
      {path: 'library/:id', component: LibraryDetailComponent},
      {path: 'library/:libraryId/series/:seriesId', component: SeriesDetailComponent},
      {path: 'library/:libraryId/series/:seriesId/manga/:volumeId', component: MangaReaderComponent},
    ]
  },
  {path: 'login', component: UserLoginComponent},
  {path: '**', component: HomeComponent, pathMatch: 'full'}
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
