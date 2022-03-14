import { BrowserModule, Title } from '@angular/platform-browser';
import { NgModule } from '@angular/core';
import { APP_BASE_HREF } from '@angular/common';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { HttpClientModule, HTTP_INTERCEPTORS } from '@angular/common/http';
import {
  NgbAccordionModule, NgbCollapseModule, NgbDropdownModule, NgbNavModule, NgbPaginationModule, NgbPopoverModule, NgbRatingModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { NavHeaderComponent } from './nav-header/nav-header.component';
import { JwtInterceptor } from './_interceptors/jwt.interceptor';
import { UserLoginComponent } from './user-login/user-login.component';
import { ToastrModule } from 'ngx-toastr';
import { ErrorInterceptor } from './_interceptors/error.interceptor';
import { LibraryComponent } from './library/library.component';
import { SharedModule } from './shared/shared.module';
import { LibraryDetailComponent } from './library-detail/library-detail.component';
import { SeriesDetailComponent } from './series-detail/series-detail.component';
import { ReviewSeriesModalComponent } from './_modals/review-series-modal/review-series-modal.component';
import { CarouselModule } from './carousel/carousel.module';

import { TypeaheadModule } from './typeahead/typeahead.module';
import { RecentlyAddedComponent } from './recently-added/recently-added.component';
import { OnDeckComponent } from './on-deck/on-deck.component';
import { DashboardComponent } from './dashboard/dashboard.component';
import { CardsModule } from './cards/cards.module';
import { CollectionsModule } from './collections/collections.module';
import { ReadingListModule } from './reading-list/reading-list.module';
import { SAVER, getSaver } from './shared/_providers/saver.provider';
import { NavEventsToggleComponent } from './nav-events-toggle/nav-events-toggle.component';
import { SeriesMetadataDetailComponent } from './series-metadata-detail/series-metadata-detail.component';
import { AllSeriesComponent } from './all-series/all-series.component';
import { RegistrationModule } from './registration/registration.module';
import { GroupedTypeaheadComponent } from './grouped-typeahead/grouped-typeahead.component';
import { ThemeTestComponent } from './theme-test/theme-test.component';
import { PipeModule } from './pipe/pipe.module';
import { ColorPickerModule } from 'ngx-color-picker';
import { SidenavModule } from './sidenav/sidenav.module';


@NgModule({
  declarations: [
    AppComponent,
    NavHeaderComponent,
    UserLoginComponent,
    LibraryComponent,
    LibraryDetailComponent,
    SeriesDetailComponent,
    ReviewSeriesModalComponent,
    RecentlyAddedComponent,
    OnDeckComponent,
    DashboardComponent,
    NavEventsToggleComponent,
    SeriesMetadataDetailComponent,
    AllSeriesComponent,
    GroupedTypeaheadComponent,
    ThemeTestComponent,
  ],
  imports: [
    HttpClientModule,
    BrowserModule,
    AppRoutingModule,
    BrowserAnimationsModule,
    ReactiveFormsModule,
    FormsModule, // EditCollection Modal

    NgbDropdownModule, // Nav
    NgbPopoverModule, // Nav Events toggle
    NgbRatingModule, // Series Detail & Filter
    NgbNavModule,
    NgbPaginationModule,

    NgbCollapseModule, // Login

    SharedModule,
    CarouselModule,
    TypeaheadModule,
    CardsModule,
    CollectionsModule,
    ReadingListModule,
    RegistrationModule,

    ColorPickerModule, // User preferences

    NgbAccordionModule, // ThemeTest Component only
    PipeModule,

    PipeModule,
    SidenavModule, // For sidenav


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
    Title,
    {provide: SAVER, useFactory: getSaver},
    // { provide: APP_BASE_HREF, useFactory: (config: ConfigData) => config.baseUrl, deps: [ConfigData] },
  ],
  entryComponents: [],
  bootstrap: [AppComponent]
})
export class AppModule { }
