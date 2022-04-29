import { BrowserModule, Title } from '@angular/platform-browser';
import { NgModule } from '@angular/core';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { HttpClientModule, HTTP_INTERCEPTORS } from '@angular/common/http';
import {
  NgbAccordionModule, NgbNavModule } from '@ng-bootstrap/ng-bootstrap';
import { JwtInterceptor } from './_interceptors/jwt.interceptor';
import { ToastrModule } from 'ngx-toastr';
import { ErrorInterceptor } from './_interceptors/error.interceptor';

import { TypeaheadModule } from './typeahead/typeahead.module';
import { CardsModule } from './cards/cards.module';
import { SAVER, getSaver } from './shared/_providers/saver.provider';
import { ThemeTestComponent } from './theme-test/theme-test.component';
import { PipeModule } from './pipe/pipe.module';
import { SidenavModule } from './sidenav/sidenav.module';
import { NavModule } from './nav/nav.module';



@NgModule({
  declarations: [
    AppComponent,

    ThemeTestComponent, // TODO: Move to a Test module or something so it's not initially loaded as it's just for devs
  ],
  imports: [
    HttpClientModule,
    BrowserModule,
    AppRoutingModule,
    BrowserAnimationsModule,

    //ReactiveFormsModule,
    //FormsModule, // EditCollection Modal

    //SharedModule, // Comes from NavModule
    //CarouselModule,
    TypeaheadModule,
    
    CardsModule, // ThemeTest Component only
    NgbAccordionModule, // ThemeTest Component only
    NgbNavModule, // ThemeTest Component only


    PipeModule,
    SidenavModule, // For sidenav
    
    NavModule,


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
  ],
  entryComponents: [],
  bootstrap: [AppComponent]
})
export class AppModule { }
