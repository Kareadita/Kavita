import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ConfirmEmailComponent } from './_components/confirm-email/confirm-email.component';
import { RegistrationRoutingModule } from '../_routes/registration.router.module';
import { NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { ReactiveFormsModule } from '@angular/forms';
import { UserLoginComponent } from './user-login/user-login.component';
import { ConfirmEmailChangeComponent } from './_components/confirm-email-change/confirm-email-change.component';
import { ConfirmMigrationEmailComponent } from './_components/confirm-migration-email/confirm-migration-email.component';
import { ConfirmResetPasswordComponent } from './_components/confirm-reset-password/confirm-reset-password.component';
import { RegisterComponent } from './_components/register/register.component';
import { ResetPasswordComponent } from './_components/reset-password/reset-password.component';
import { SplashContainerComponent } from './_components/splash-container/splash-container.component';
import {TranslocoModule} from "@ngneat/transloco";



@NgModule({
    imports: [
        CommonModule,
        RegistrationRoutingModule,
        NgbTooltipModule,
        ReactiveFormsModule,
        ConfirmEmailComponent,
        SplashContainerComponent,
        RegisterComponent,
        ConfirmMigrationEmailComponent,
        ResetPasswordComponent,
        ConfirmResetPasswordComponent,
        UserLoginComponent,
        ConfirmEmailChangeComponent,
        TranslocoModule
    ],
    exports: [
        SplashContainerComponent
    ],
})
export class RegistrationModule { }
