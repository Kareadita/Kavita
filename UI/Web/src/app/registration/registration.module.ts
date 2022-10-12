import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ConfirmEmailComponent } from './confirm-email/confirm-email.component';
import { RegistrationRoutingModule } from './registration.router.module';
import { NgbCollapseModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { ReactiveFormsModule } from '@angular/forms';
import { SplashContainerComponent } from './splash-container/splash-container.component';
import { RegisterComponent } from './register/register.component';
import { AddEmailToAccountMigrationModalComponent } from './add-email-to-account-migration-modal/add-email-to-account-migration-modal.component';
import { ConfirmMigrationEmailComponent } from './confirm-migration-email/confirm-migration-email.component';
import { ResetPasswordComponent } from './reset-password/reset-password.component';
import { ConfirmResetPasswordComponent } from './confirm-reset-password/confirm-reset-password.component';
import { UserLoginComponent } from './user-login/user-login.component';
import { ConfirmEmailChangeComponent } from './confirm-email-change/confirm-email-change.component';



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
