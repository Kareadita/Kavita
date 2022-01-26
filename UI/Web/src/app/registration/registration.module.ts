import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ConfirmEmailComponent } from './confirm-email/confirm-email.component';
import { RegistrationRoutingModule } from './registration.router.module';
import { NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { ReactiveFormsModule } from '@angular/forms';
import { SplashContainerComponent } from './splash-container/splash-container.component';
import { RegisterComponent } from './register/register.component';



@NgModule({
  declarations: [
    ConfirmEmailComponent,
    SplashContainerComponent,
    RegisterComponent
  ],
  imports: [
    CommonModule,
    RegistrationRoutingModule,
    NgbTooltipModule,
    ReactiveFormsModule
  ]
})
export class RegistrationModule { }
