import { NgModule } from '@angular/core';
import { Routes, RouterModule, PreloadAllModules } from '@angular/router';
import { AuthGuard } from './_guards/auth.guard';
import { LibraryAccessGuard } from './_guards/library-access.guard';
import { AdminGuard } from './_guards/admin.guard';

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
  // {
  //   path: 'bookmarks',
  //   loadChildren: () => import('../app/bookmark/bookmark.module').then(m => m.BookmarkModule)
  // },
  {
    path: 'bookmarks',
    loadChildren: () => import('../app/bookmark/bookmark-routing.module').then(m => m.routes)
  },
  {
    path: 'all-series',
    loadChildren: () => import('../app/all-series/all-series.module').then(m => m.AllSeriesModule)
  },
  {
    path: 'libraries',
    loadChildren: () => import('./dashboard/dashboard.module').then(m => m.DashboardModule)
  },
  {
    path: 'libraries',
    loadChildren: () => import('./dashboard/dashboard.module').then(m => m.DashboardModule)
  },
  {
    path: 'want-to-read',
    loadChildren: () => import('../app/want-to-read/want-to-read.module').then(m => m.WantToReadModule)
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
        loadChildren: () => import('../app/series-detail/series-detail.module').then(m => m.SeriesDetailModule)
      },
      {
        path: ':libraryId/series/:seriesId/manga',
        loadChildren: () => import('../app/manga-reader/manga-reader.module').then(m => m.MangaReaderModule)
      },
      {
        path: ':libraryId/series/:seriesId/book',
        loadChildren: () => import('../app/book-reader/book-reader.module').then(m => m.BookReaderModule)
      },
      {
        path: ':libraryId/series/:seriesId/pdf',
        loadChildren: () => import('../app/pdf-reader/pdf-reader.module').then(m => m.PdfReaderModule)
      },
    ]
  },
  {path: 'login', loadChildren: () => import('../app/registration/registration.module').then(m => m.RegistrationModule)},
  {path: '**', pathMatch: 'full', redirectTo: 'libraries'},
  {path: '**', pathMatch: 'prefix', redirectTo: 'libraries'},
];

@NgModule({
  imports: [RouterModule.forRoot(routes, {scrollPositionRestoration: 'enabled', preloadingStrategy: PreloadAllModules})],
  exports: [RouterModule]
})
export class AppRoutingModule { }
