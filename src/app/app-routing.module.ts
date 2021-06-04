import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { AllCollectionsComponent } from './all-collections/all-collections.component';
import { HomeComponent } from './home/home.component';
import { LibraryDetailComponent } from './library-detail/library-detail.component';
import { LibraryComponent } from './library/library.component';
import { NotConnectedComponent } from './not-connected/not-connected.component';
import { SeriesDetailComponent } from './series-detail/series-detail.component';
import { RecentlyAddedComponent } from './recently-added/recently-added.component';
import { UserLoginComponent } from './user-login/user-login.component';
import { UserPreferencesComponent } from './user-preferences/user-preferences.component';
import { AuthGuard } from './_guards/auth.guard';
import { LibraryAccessGuard } from './_guards/library-access.guard';

// TODO: Once we modularize the components, use this and measure performance impact: https://angular.io/guide/lazy-loading-ngmodules#preloading-modules

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
      {
        path: 'library/:libraryId/series/:seriesId/manga',
        loadChildren: () => import('../app/manga-reader/manga-reader.module').then(m => m.MangaReaderModule)
      },
      {
        path: 'library/:libraryId/series/:seriesId/book',
        loadChildren: () => import('../app/book-reader/book-reader.module').then(m => m.BookReaderModule)
      }
    ]
  },
  {
    path: '',
    runGuardsAndResolvers: 'always',
    canActivate: [AuthGuard],
    children: [
      {path: 'recently-added', component: RecentlyAddedComponent},
      {path: 'collections', component: AllCollectionsComponent},
      {path: 'collections/:id', component: AllCollectionsComponent},
    ]
  },
  {path: 'login', component: UserLoginComponent},
  {path: 'preferences', component: UserPreferencesComponent},
  {path: 'no-connection', component: NotConnectedComponent},
  {path: '**', component: HomeComponent, pathMatch: 'full'}
];

@NgModule({
  imports: [RouterModule.forRoot(routes, {scrollPositionRestoration: 'enabled'})],
  exports: [RouterModule]
})
export class AppRoutingModule { }
