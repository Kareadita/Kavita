import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ConfirmEmailComponent } from './_components/confirm-email/confirm-email.component';
import { RegistrationRoutingModule } from './registration.router.module';
import { NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { ReactiveFormsModule } from '@angular/forms';
import { AddEmailToAccountMigrationModalComponent } from './_modals/add-email-to-account-migration-modal/add-email-to-account-migration-modal.component';
import { UserLoginComponent } from './user-login/user-login.component';
import { ConfirmEmailChangeComponent } from './_components/confirm-email-change/confirm-email-change.component';
import { ConfirmMigrationEmailComponent } from './_components/confirm-migration-email/confirm-migration-email.component';
import { ConfirmResetPasswordComponent } from './_components/confirm-reset-password/confirm-reset-password.component';
import { RegisterComponent } from './_components/register/register.component';
import { ResetPasswordComponent } from './_components/reset-password/reset-password.component';
import { SplashContainerComponent } from './_components/splash-container/splash-container.component';



@NgModule({
  declarations: [
    ConfirmEmailComponent,
    SplashContainerComponent,
    RegisterComponent,
    AddEmailToAccountMigrationModalComponent,
    ConfirmMigrationEmailComponent,
    ResetPasswordComponent,
    ConfirmResetPasswordComponent,
    UserLoginComponent,
    ConfirmEmailChangeComponent
  ],
  imports: [
    CommonModule,
    RegistrationRoutingModule,
    NgbTooltipModule,
    ReactiveFormsModule
  ],
  exports: [
    SplashContainerComponent
  ]
})
export class RegistrationModule { }
