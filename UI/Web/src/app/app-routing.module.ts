import {Routes} from '@angular/router';
import {authGuard} from './_guards/auth.guard';
import {libraryAccessGuard} from './_guards/library-access.guard';
import {libraryResolver} from "./_resolvers/library.resolver";
import {seriesResolver} from "./_resolvers/series.resolver";
import {volumeResolver} from "./_resolvers/volume.resolver";
import {chapterResolver} from "./_resolvers/chapter.resolver";
import {personResolver} from "./_resolvers/person.resolver";
import {readingListResolver} from "./_resolvers/reading-list.resolver";
import {UrlFilterResolver} from "./_resolvers/url-filter.resolver";
import {ThemeComponent} from "./single-module/theme/theme.component";

export const routes: Routes = [
  {
    path: '',
    canActivate: [authGuard],
    runGuardsAndResolvers: 'always',
    children: [
      {
        path: 'theme',
        loadChildren: () => [{path: '', component: ThemeComponent, pathMatch: 'full', title: 'Themes'}]
      },
      {
        path: 'settings',
        loadChildren: () => import('./_routes/settings-routing.module').then(m => m.routes)
      },
      {
        path: 'collections',
        loadChildren: () => import('./_routes/collections-routing.module').then(m => m.routes)
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
        path: 'person/:name',
        runGuardsAndResolvers: 'always',
        canActivate: [authGuard],
        resolve: { person: personResolver },
        loadComponent: () => import('./person-detail/person-detail.component').then(m => m.PersonDetailComponent)
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
        path: 'lists',
        pathMatch: 'full',
        title: 'title.reading-lists',
        loadComponent: () => import('./reading-list/_components/reading-lists/reading-lists.component').then(c => c.ReadingListsComponent),
        resolve: {
          filter: UrlFilterResolver
        }
      },
      {
        path: 'lists/:readingListId',
        runGuardsAndResolvers: 'always',
        canActivate: [authGuard],
        data: {titleField: 'readingList', titleProp: 'title'},
        resolve: { readingList: readingListResolver },
        loadComponent: () => import('./reading-list/_components/reading-list-detail/reading-list-detail.component').then(c => c.ReadingListDetailComponent)
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
            data: {titleField: 'series', titleProp: 'name', titleSuffix: ' Details'},
            resolve: { series: seriesResolver },
            loadComponent: () => import('./series-detail/_components/series-detail/series-detail.component').then(c => c.default)
          },
          {
            path: 'series/:seriesId/volume/:volumeId',
            pathMatch: 'full',
            resolve: { series: seriesResolver, volume: volumeResolver },
            loadComponent: () => import('./volume-detail/volume-detail.component').then(c => c.VolumeDetailComponent)
          },
          {
            path: 'series/:seriesId/chapter/:chapterId',
            pathMatch: 'full',
            resolve: { series: seriesResolver, chapter: chapterResolver },
            loadComponent: () => import('./chapter-detail/chapter-detail.component').then(c => c.ChapterDetailComponent)
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
