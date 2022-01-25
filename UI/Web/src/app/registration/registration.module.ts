import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ConfirmEmailComponent } from './confirm-email/confirm-email.component';
import { RegistrationRoutingModule } from './registration.router.module';
import { NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { ReactiveFormsModule } from '@angular/forms';
import { SplashContainerComponent } from './splash-container/splash-container.component';



@NgModule({
  declarations: [
    ConfirmEmailComponent,
    SplashContainerComponent
  ],
  imports: [
    CommonModule,
    RegistrationRoutingModule,
    NgbTooltipModule,
    ReactiveFormsModule
  ]
})
export class RegistrationModule { }
