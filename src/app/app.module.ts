import { BrowserModule, Title } from '@angular/platform-browser';
import { APP_INITIALIZER, ErrorHandler, NgModule } from '@angular/core';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { HomeComponent } from './home/home.component';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { HttpClientModule, HTTP_INTERCEPTORS } from '@angular/common/http';
import { NgbAccordionModule, NgbCollapseModule, NgbDropdownModule, NgbNavModule, NgbPaginationModule, NgbRatingModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { NavHeaderComponent } from './nav-header/nav-header.component';
import { JwtInterceptor } from './_interceptors/jwt.interceptor';
import { UserLoginComponent } from './user-login/user-login.component';
import { ToastrModule } from 'ngx-toastr';
import { ErrorInterceptor } from './_interceptors/error.interceptor';
import { LibraryComponent } from './library/library.component';
import { SharedModule } from './shared/shared.module';
import { LibraryDetailComponent } from './library-detail/library-detail.component';
import { SeriesDetailComponent } from './series-detail/series-detail.component';
import { NotConnectedComponent } from './not-connected/not-connected.component';
import { UserPreferencesComponent } from './user-preferences/user-preferences.component';
import { AutocompleteLibModule } from 'angular-ng-autocomplete';
import { EditSeriesModalComponent } from './_modals/edit-series-modal/edit-series-modal.component';
import { ReviewSeriesModalComponent } from './_modals/review-series-modal/review-series-modal.component';
import { LazyLoadImageModule} from 'ng-lazyload-image';
import { CarouselModule } from './carousel/carousel.module';
import { NgxSliderModule } from '@angular-slider/ngx-slider';

import * as Sentry from '@sentry/angular';
import { environment } from 'src/environments/environment';
import { version } from 'package.json';
import { Router } from '@angular/router';
import { RewriteFrames as RewriteFramesIntegration } from '@sentry/integrations';
import { Dedupe as DedupeIntegration } from '@sentry/integrations';
import { PersonBadgeComponent } from './person-badge/person-badge.component';
import { TypeaheadModule } from './typeahead/typeahead.module';
import { AllCollectionsComponent } from './all-collections/all-collections.component';
import { EditCollectionTagsComponent } from './_modals/edit-collection-tags/edit-collection-tags.component';
import { RecentlyAddedComponent } from './recently-added/recently-added.component';

let sentryProviders: any[] = [];

if (environment.production) {
  Sentry.init({
    dsn: 'https://db1a1f6445994b13a6f479512aecdd48@o641015.ingest.sentry.io/5757426',
    environment: environment.production ? 'prod' : 'dev',
    release: version,
    integrations: [
      new Sentry.Integrations.GlobalHandlers({
        onunhandledrejection: true,
        onerror: true
      }),
      new DedupeIntegration(),
      new RewriteFramesIntegration(),
    ],
    ignoreErrors: [new RegExp(/\/api\/admin/)],
    tracesSampleRate: 0,
  });

  Sentry.configureScope(scope => {
    scope.setUser({
      username: 'Not authorized'
    });
    scope.setTag('production', environment.production);
    scope.setTag('version', version);
  });

  sentryProviders = [{
    provide: ErrorHandler,
    useValue: Sentry.createErrorHandler({
      showDialog: false,
    }),
  },
  {
    provide: Sentry.TraceService,
    deps: [Router],
  },
  {
    provide: APP_INITIALIZER,
    useFactory: () => () => {},
    deps: [Sentry.TraceService],
    multi: true,
  }];
}

@NgModule({
  declarations: [
    AppComponent,
    HomeComponent,
    NavHeaderComponent,
    UserLoginComponent,
    LibraryComponent, 
    LibraryDetailComponent, 
    SeriesDetailComponent, 
    NotConnectedComponent, // Move into ExtrasModule
    UserPreferencesComponent, // Move into SettingsModule
    EditSeriesModalComponent,
    ReviewSeriesModalComponent,
    PersonBadgeComponent,
    AllCollectionsComponent,
    EditCollectionTagsComponent,
    RecentlyAddedComponent,
  ],
  imports: [
    HttpClientModule,
    BrowserModule,
    AppRoutingModule,
    BrowserAnimationsModule,
    ReactiveFormsModule,
    NgbDropdownModule, // Nav
    AutocompleteLibModule, // Nav
    NgbTooltipModule, // Shared & SettingsModule
    NgbRatingModule, // Series Detail
    NgbCollapseModule, // Series Edit Modal
    NgbNavModule, // Series Edit Modal
    NgbAccordionModule, // User Preferences
    NgxSliderModule, // User Preference
    NgbPaginationModule,
    LazyLoadImageModule,
    SharedModule,
    CarouselModule,
    TypeaheadModule,
    FormsModule, // EditCollection Modal
    ToastrModule.forRoot({
      positionClass: 'toast-bottom-right',
      preventDuplicates: true,
      timeOut: 6000,
      countDuplicates: true,
      autoDismiss: true
    }),
  ],
  providers: [
    {provide: HTTP_INTERCEPTORS, useClass: ErrorInterceptor, multi: true},
    {provide: HTTP_INTERCEPTORS, useClass: JwtInterceptor, multi: true},
    //{ provide: LAZYLOAD_IMAGE_HOOKS, useClass: ScrollHooks } // Great, but causes flashing after modals close
    Title,
    ...sentryProviders,
  ],
  entryComponents: [],
  bootstrap: [AppComponent]
})
export class AppModule { }
