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
import { SidenavModule } from './sidenav/sidenav.module';
import { NavModule } from './nav/nav.module';




@NgModule({
    declarations: [
        AppComponent,
    ],
    imports: [
        HttpClientModule,
        BrowserModule,
        AppRoutingModule,
        BrowserAnimationsModule,
        SidenavModule,
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
        { provide: HTTP_INTERCEPTORS, useClass: ErrorInterceptor, multi: true },
        { provide: HTTP_INTERCEPTORS, useClass: JwtInterceptor, multi: true },
        Title,
        { provide: SAVER, useFactory: getSaver },
    ],
    bootstrap: [AppComponent]
})
export class AppModule { }
