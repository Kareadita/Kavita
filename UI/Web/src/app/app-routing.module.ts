import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { LibraryDetailComponent } from './library-detail/library-detail.component';
import { NotConnectedComponent } from './not-connected/not-connected.component';
import { SeriesDetailComponent } from './series-detail/series-detail.component';
import { RecentlyAddedComponent } from './recently-added/recently-added.component';
import { UserLoginComponent } from './user-login/user-login.component';
import { AuthGuard } from './_guards/auth.guard';
import { LibraryAccessGuard } from './_guards/library-access.guard';
import { OnDeckComponent } from './on-deck/on-deck.component';
import { DashboardComponent } from './dashboard/dashboard.component';
import { AllSeriesComponent } from './all-series/all-series.component';
import { AdminGuard } from './_guards/admin.guard';

// TODO: Once we modularize the components, use this and measure performance impact: https://angular.io/guide/lazy-loading-ngmodules#preloading-modules

const routes: Routes = [
  {path: '', component: UserLoginComponent},
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
      {path: 'library', component: DashboardComponent},
      {path: 'recently-added', component: RecentlyAddedComponent},
      {path: 'on-deck', component: OnDeckComponent},
      {path: 'all-series', component: AllSeriesComponent},

    ]
  },
  {
    path: 'registration',
    loadChildren: () => import('../app/registration/registration.module').then(m => m.RegistrationModule)
  },
  {path: 'login', component: UserLoginComponent}, // TODO: move this to registration module
  {path: 'no-connection', component: NotConnectedComponent},
  {path: '**', component: UserLoginComponent, pathMatch: 'full'}
];

@NgModule({
  imports: [RouterModule.forRoot(routes, {scrollPositionRestoration: 'enabled'})],
  exports: [RouterModule]
})
export class AppRoutingModule { }
