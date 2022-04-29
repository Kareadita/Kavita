import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { LibraryDetailComponent } from './library-detail/library-detail.component';
import { SeriesDetailComponent } from './series-detail/series-detail.component';
import { UserLoginComponent } from './user-login/user-login.component';
import { AuthGuard } from './_guards/auth.guard';
import { LibraryAccessGuard } from './_guards/library-access.guard';
import { DashboardComponent } from './dashboard/dashboard.component';
import { AdminGuard } from './_guards/admin.guard';
import { ThemeTestComponent } from './theme-test/theme-test.component';

// TODO: Once we modularize the components, use this and measure performance impact: https://angular.io/guide/lazy-loading-ngmodules#preloading-modules
// TODO: Use Prefetching of LazyLoaded Modules 
const routes: Routes = [
  {
    path: 'admin',
    canActivate: [AdminGuard],
    loadChildren: () => import('./admin/admin.module').then(m => m.AdminModule)
  },
  {
    path: 'collections',
    canActivate: [AuthGuard],
    loadChildren: () => import('./collections/collections.module').then(m => m.CollectionsModule)
  },
  {
    path: 'preferences',
    canActivate: [AuthGuard],
    loadChildren: () => import('./user-settings/user-settings.module').then(m => m.UserSettingsModule)
  },
  {
    path: 'lists',
    canActivate: [AuthGuard],
    loadChildren: () => import('./reading-list/reading-list.module').then(m => m.ReadingListModule)
  },
  {
    path: 'registration',
    loadChildren: () => import('../app/registration/registration.module').then(m => m.RegistrationModule)
  },
  {
    path: 'announcements',
    loadChildren: () => import('../app/announcements/announcements.module').then(m => m.AnnouncementsModule)
  },
  {
    path: 'bookmarks',
    loadChildren: () => import('../app/bookmark/bookmark.module').then(m => m.BookmarkModule)
  },
  {
    path: 'all-series',
    loadChildren: () => import('../app/all-series/all-series.module').then(m => m.AllSeriesModule)
  },
  {
    path: 'libraries',
    loadChildren: () => import('../app/dashboard/dashboard.module').then(m => m.DashboardModule)
  },
  {
    path: 'library',
    runGuardsAndResolvers: 'always',
    canActivate: [AuthGuard, LibraryAccessGuard],
    children: [
      {
        path: ':libraryId', 
        pathMatch: 'full',
        loadChildren: () => import('../app/library-detail/library-detail.module').then(m => m.LibraryDetailModule)
      },
      {
        path: ':libraryId/series/:seriesId', 
        pathMatch: 'full',
        component: SeriesDetailComponent
      },
      {
        path: ':libraryId/series/:seriesId/manga',
        loadChildren: () => import('../app/manga-reader/manga-reader.module').then(m => m.MangaReaderModule)
      },
      {
        path: ':libraryId/series/:seriesId/book',
        loadChildren: () => import('../app/book-reader/book-reader.module').then(m => m.BookReaderModule)
      },
    ]
  },
  {path: 'theme', component: ThemeTestComponent},

  {path: 'login', component: UserLoginComponent},

  {path: '**', component: UserLoginComponent, pathMatch: 'full'},
  {path: '', component: UserLoginComponent},
];

@NgModule({
  imports: [RouterModule.forRoot(routes, {scrollPositionRestoration: 'enabled'})],
  exports: [RouterModule]
})
export class AppRoutingModule { }
