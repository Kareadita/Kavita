import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ConfirmEmailComponent } from './confirm-email/confirm-email.component';
import { RegistrationRoutingModule } from './registration.router.module';
import { NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { ReactiveFormsModule } from '@angular/forms';
import { SplashContainerComponent } from './splash-container/splash-container.component';
import { RegisterComponent } from './register/register.component';
import { AddEmailToAccountMigrationModalComponent } from './add-email-to-account-migration-modal/add-email-to-account-migration-modal.component';
import { ConfirmMigrationEmailComponent } from './confirm-migration-email/confirm-migration-email.component';



@NgModule({
  declarations: [
    ConfirmEmailComponent,
    SplashContainerComponent,
    RegisterComponent,
    AddEmailToAccountMigrationModalComponent,
    ConfirmMigrationEmailComponent
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
