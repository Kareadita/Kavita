import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { HomeComponent } from './home/home.component';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { HttpClientModule, HTTP_INTERCEPTORS } from '@angular/common/http';
import { NgbButtonsModule, NgbDropdownModule, NgbModalModule, NgbModule, NgbProgressbarModule, NgbRatingModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { NavHeaderComponent } from './nav-header/nav-header.component';
import { JwtInterceptor } from './_interceptors/jwt.interceptor';
import { UserLoginComponent } from './user-login/user-login.component';
import { ToastrModule } from 'ngx-toastr';
import { ErrorInterceptor } from './_interceptors/error.interceptor';
import { LibraryComponent } from './library/library.component';
import { SharedModule } from './shared/shared.module';
import { LibraryDetailComponent } from './library-detail/library-detail.component';
import { SeriesDetailComponent } from './series-detail/series-detail.component';
import { MangaReaderComponent } from './manga-reader/manga-reader.component';
import { NotConnectedComponent } from './not-connected/not-connected.component';
import { UserPreferencesComponent } from './user-preferences/user-preferences.component';




@NgModule({
  declarations: [
    AppComponent,
    HomeComponent,
    NavHeaderComponent,
    UserLoginComponent,
    LibraryComponent, // Move into MangaModule
    LibraryDetailComponent, // Move into MangaModule
    SeriesDetailComponent, // Move into MangaModule
    MangaReaderComponent, // Move into ReadersModule
    NotConnectedComponent, // Move into ExtrasModule
    UserPreferencesComponent, // Move into SettingsModule
  ],
  imports: [
    HttpClientModule,
    BrowserModule,
    AppRoutingModule,
    BrowserAnimationsModule,
    FormsModule, // Just used for gotopage; TODO: Remove this and use ReactiveForms (this module is large)
    ReactiveFormsModule,
    NgbModalModule,
    NgbButtonsModule,
    NgbDropdownModule,
    NgbTooltipModule,
    NgbRatingModule,
    NgbProgressbarModule,
    SharedModule,
    ToastrModule.forRoot({
      positionClass: 'toast-bottom-right'
    }),
  ],
  providers: [
    {provide: HTTP_INTERCEPTORS, useClass: ErrorInterceptor, multi: true},
    {provide: HTTP_INTERCEPTORS, useClass: JwtInterceptor, multi: true}
  ],
  entryComponents: [],
  bootstrap: [AppComponent]
})
export class AppModule { }
