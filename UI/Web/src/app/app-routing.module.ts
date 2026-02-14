import {Routes} from '@angular/router';
import {AuthGuard} from './_guards/auth.guard';
import {libraryAccessGuard} from './_guards/library-access.guard';
import {libraryResolver} from "./_resolvers/library.resolver";
import {seriesResolver} from "./_resolvers/series.resolver";

export const routes: Routes = [
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
        path: 'browse',
        loadChildren: () => import('./_routes/browse-routing.module').then(m => m.routes)
      },
      {
        path: 'profile',
        loadChildren: () => import('./_routes/profile-routing.module').then(m => m.routes)
      },
      {
        path: 'library/:libraryId',
        runGuardsAndResolvers: 'always',
        canActivate: [libraryAccessGuard],
        resolve: { library: libraryResolver },
        children: [
          {
            path: '',
            pathMatch: 'full',
            loadChildren: () => import('./_routes/library-detail-routing.module').then(m => m.routes)
          },
          {
            path: 'series/:seriesId',
            pathMatch: 'full',
            resolve: { series: seriesResolver },
            loadComponent: () => import('./series-detail/_components/series-detail/series-detail.component').then(c => c.default)
          },
          {
            path: 'series/:seriesId/chapter/:chapterId',
            pathMatch: 'full',
            loadComponent: () => import('./chapter-detail/chapter-detail.component').then(c => c.ChapterDetailComponent)
          },
          {
            path: 'series/:seriesId/volume/:volumeId',
            pathMatch: 'full',
            loadComponent: () => import('./volume-detail/volume-detail.component').then(c => c.VolumeDetailComponent)
          },
          {
            path: 'series/:seriesId/manga',
            loadChildren: () => import('./_routes/manga-reader.router.module').then(m => m.routes)
          },
          {
            path: 'series/:seriesId/book',
            loadChildren: () => import('./_routes/book-reader.router.module').then(m => m.routes)
          },
          {
            path: 'series/:seriesId/pdf',
            loadChildren: () => import('./_routes/pdf-reader.router.module').then(m => m.routes)
          }
        ]
      },
      { path: '', pathMatch: 'full', redirectTo: 'home' }
    ]
  },
  {
    path: 'registration',
    loadChildren: () => import('./_routes/registration.router.module').then(m => m.routes)
  },
  {
    path: 'login',
    loadChildren: () => import('./_routes/registration.router.module').then(m => m.routes)
  },
  { path: 'libraries', pathMatch: 'full', redirectTo: 'home' },
  { path: '**', redirectTo: 'home' }
];
