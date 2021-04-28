import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { HomeComponent } from './home/home.component';
import { ReactiveFormsModule } from '@angular/forms';
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




@NgModule({
  declarations: [
    AppComponent,
    HomeComponent,
    NavHeaderComponent,
    UserLoginComponent,
    LibraryComponent, // Move into MangaModule
    LibraryDetailComponent, // Move into MangaModule
    SeriesDetailComponent, // Move into MangaModule
    NotConnectedComponent, // Move into ExtrasModule
    UserPreferencesComponent, EditSeriesModalComponent, ReviewSeriesModalComponent, // Move into SettingsModule
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
    ToastrModule.forRoot({
      positionClass: 'toast-bottom-right'
    }),
  ],
  providers: [
    {provide: HTTP_INTERCEPTORS, useClass: ErrorInterceptor, multi: true},
    {provide: HTTP_INTERCEPTORS, useClass: JwtInterceptor, multi: true},
    //{ provide: LAZYLOAD_IMAGE_HOOKS, useClass: ScrollHooks } // Great, but causes flashing after modals close
  ],
  entryComponents: [],
  bootstrap: [AppComponent]
})
export class AppModule { }
