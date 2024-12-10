import { NgModule } from '@angular/core';
import { Routes, RouterModule, PreloadAllModules } from '@angular/router';
import { AuthGuard } from './_guards/auth.guard';
import { LibraryAccessGuard } from './_guards/library-access.guard';
import { AdminGuard } from './_guards/admin.guard';

const routes: Routes = [
  {
    path: '',
    canActivate: [AuthGuard],
    runGuardsAndResolvers: 'always',
    children: [
      {
        path: 'settings',
        loadChildren: () => import('./_routes/settings-routing.module').then(m => m.routes)
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
        path: 'all-filters',
        loadChildren: () => import('./_routes/all-filters-routing.module').then(m => m.routes)
      },
      {
        path: 'want-to-read',
        loadChildren: () => import('./_routes/want-to-read-routing.module').then(m => m.routes)
      },
      {
        path: 'home',
        loadChildren: () => import('./_routes/dashboard-routing.module').then(m => m.routes)
      },
      {
        path: 'person',
        loadChildren: () => import('./_routes/person-detail-routing.module').then(m => m.routes)
      },
      {
        path: 'browse/authors',
        loadChildren: () => import('./_routes/browse-authors-routing.module').then(m => m.routes)
      },
      {
        path: 'library',
        runGuardsAndResolvers: 'always',
        canActivate: [AuthGuard, LibraryAccessGuard],
        children: [
          {
            path: ':libraryId',
            pathMatch: 'full',
            loadChildren: () => import('./_routes/library-detail-routing.module').then(m => m.routes)
          },
          {
            path: ':libraryId/series/:seriesId',
            pathMatch: 'full',
            loadComponent: () => import('../app/series-detail/_components/series-detail/series-detail.component').then(c => c.SeriesDetailComponent)
          },
          {
            path: ':libraryId/series/:seriesId/chapter/:chapterId',
            pathMatch: 'full',
            loadComponent: () => import('./chapter-detail/chapter-detail.component').then(c => c.ChapterDetailComponent)
          },
          {
            path: ':libraryId/series/:seriesId/volume/:volumeId',
            pathMatch: 'full',
            loadComponent: () => import('./volume-detail/volume-detail.component').then(c => c.VolumeDetailComponent)
          },
          {
            path: ':libraryId/series/:seriesId/manga',
            loadChildren: () => import('./_routes/manga-reader.router.module').then(m => m.routes)
          },
          {
            path: ':libraryId/series/:seriesId/book',
            loadChildren: () => import('./_routes/book-reader.router.module').then(m => m.routes)
          },
          {
            path: ':libraryId/series/:seriesId/pdf',
            loadChildren: () => import('./_routes/pdf-reader.router.module').then(m => m.routes)
          },
        ]
      },
      {path: '', pathMatch: 'full', redirectTo: 'home'},
    ]
  },
  {
    path: 'registration',
    loadChildren: () => import('./_routes/registration.router.module').then(m => m.routes)
  },
  {
    path: 'login',
    loadChildren: () => import('./_routes/registration.router.module').then(m => m.routes) // TODO: Refactor so we just use /registration/login going forward
  },
  {path: 'libraries', pathMatch: 'full', redirectTo: 'home'},
  {path: '**', pathMatch: 'prefix', redirectTo: 'home'},
  {path: '**', pathMatch: 'full', redirectTo: 'home'},
  {path: '', pathMatch: 'full', redirectTo: 'home'},
];

@NgModule({
  imports: [RouterModule.forRoot(routes, {scrollPositionRestoration: 'enabled', preloadingStrategy: PreloadAllModules})],
  exports: [RouterModule]
})
export class AppRoutingModule { }
