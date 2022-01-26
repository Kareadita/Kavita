import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { ConfirmEmailComponent } from './confirm-email/confirm-email.component';
import { RegisterComponent } from './register/register.component';

const routes: Routes = [
  {
      path: 'confirm-email',
      component: ConfirmEmailComponent,
  },
  {
    path: 'register',
    component: RegisterComponent,
  }
];


@NgModule({
    imports: [RouterModule.forChild(routes), ],
    exports: [RouterModule]
})
export class RegistrationRoutingModule { }
