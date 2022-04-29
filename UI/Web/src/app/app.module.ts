import { BrowserModule, Title } from '@angular/platform-browser';
import { NgModule } from '@angular/core';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { HttpClientModule, HTTP_INTERCEPTORS } from '@angular/common/http';
import {
  NgbAccordionModule, NgbDropdownModule, NgbNavModule, NgbPopoverModule } from '@ng-bootstrap/ng-bootstrap';
import { NavHeaderComponent } from './nav-header/nav-header.component';
import { JwtInterceptor } from './_interceptors/jwt.interceptor';
import { ToastrModule } from 'ngx-toastr';
import { ErrorInterceptor } from './_interceptors/error.interceptor';
import { SharedModule } from './shared/shared.module';
import { CarouselModule } from './carousel/carousel.module';

import { TypeaheadModule } from './typeahead/typeahead.module';
import { CardsModule } from './cards/cards.module';
import { SAVER, getSaver } from './shared/_providers/saver.provider';
import { EventsWidgetComponent } from './events-widget/events-widget.component';
import { GroupedTypeaheadComponent } from './grouped-typeahead/grouped-typeahead.component';
import { ThemeTestComponent } from './theme-test/theme-test.component';
import { PipeModule } from './pipe/pipe.module';
import { SidenavModule } from './sidenav/sidenav.module';



@NgModule({
  declarations: [
    AppComponent,
    
    NavHeaderComponent, // TODO: Move to NavModule 
    EventsWidgetComponent, // TODO: Move to NavModule 
    GroupedTypeaheadComponent, // TODO: Move to NavModule 

    ThemeTestComponent, // TODO: Move to a Test module or something so it's not initially loaded as it's just for devs
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
    NgbNavModule,


    SharedModule,
    CarouselModule,
    TypeaheadModule,
    
    CardsModule,

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
  ],
  entryComponents: [],
  bootstrap: [AppComponent]
})
export class AppModule { }
