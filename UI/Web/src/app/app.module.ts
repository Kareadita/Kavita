import { BrowserModule, Title } from '@angular/platform-browser';
import { NgModule } from '@angular/core';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { HttpClientModule, HTTP_INTERCEPTORS } from '@angular/common/http';
import { JwtInterceptor } from './_interceptors/jwt.interceptor';
import { ToastrModule } from 'ngx-toastr';
import { ErrorInterceptor } from './_interceptors/error.interceptor';

import { SAVER, getSaver } from './shared/_providers/saver.provider';
import { NavModule } from './nav/nav.module';
import {SideNavComponent} from "./sidenav/_components/side-nav/side-nav.component";



// Disable Web Animations if the user's browser (such as iOS 12.5.5) does not support this.
const disableAnimations = !('animate' in document.documentElement);
if (disableAnimations) console.error("Web Animations have been disabled as your current browser does not support this.");


@NgModule({
    declarations: [
        AppComponent,
    ],
  imports: [
    HttpClientModule,
    BrowserModule,
    AppRoutingModule,
    BrowserAnimationsModule.withConfig({disableAnimations}),
    NavModule,
    ToastrModule.forRoot({
      positionClass: 'toast-bottom-right',
      preventDuplicates: true,
      timeOut: 6000,
      countDuplicates: true,
      autoDismiss: true
    }),
    SideNavComponent,
  ],
    providers: [
        { provide: HTTP_INTERCEPTORS, useClass: ErrorInterceptor, multi: true },
        { provide: HTTP_INTERCEPTORS, useClass: JwtInterceptor, multi: true },
        Title,
        { provide: SAVER, useFactory: getSaver },
    ],
    bootstrap: [AppComponent]
})
export class AppModule { }
