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
    path: 'preferences',
    canActivate: [AuthGuard],
    loadChildren: () => import('./user-settings/user-settings.module').then(m => m.UserSettingsModule)
  },
  {
    path: 'collections',
    loadChildren: () => import('./_routes/collections-routing.module').then(m => m.routes)
  },
  {
    path: 'lists',
    loadChildren: () => import('./_routes/reading-list-routing.module').then(m => m.routes)
  },
  {
    path: 'registration',
    loadChildren: () => import('./_routes/registration.router.module').then(m => m.routes)
  },
  {
    path: 'login',
    loadChildren: () => import('./_routes/registration.router.module').then(m => m.routes) // TODO: Refactor so we just use /registration/login going forward
  },
  {
    path: 'announcements',
    loadChildren: () => import('./_routes/announcements-routing.module').then(m => m.routes)
  },
  {
    path: 'bookmarks',
    loadChildren: () => import('./_routes/bookmark-routing.module').then(m => m.routes)
  },
  {
    path: 'all-series',
    loadChildren: () => import('./_routes/all-series-routing.module').then(m => m.routes)
  },
  {
    path: 'want-to-read',
    loadChildren: () => import('./_routes/want-to-read-routing.module').then(m => m.routes)
  },
  {
    path: 'libraries', // TODO: libraries/ route is deprecated, we are switching to /home as it makes much more sense
    loadChildren: () => import('./_routes/dashboard-routing.module').then(m => m.routes)
  },
  {
    path: 'home',
    loadChildren: () => import('./_routes/dashboard-routing.module').then(m => m.routes)
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
  {path: '**', pathMatch: 'full', redirectTo: 'home'},
  {path: '**', pathMatch: 'prefix', redirectTo: 'home'},
];

@NgModule({
  imports: [RouterModule.forRoot(routes, {scrollPositionRestoration: 'enabled', preloadingStrategy: PreloadAllModules})],
  exports: [RouterModule]
})
export class AppRoutingModule { }
