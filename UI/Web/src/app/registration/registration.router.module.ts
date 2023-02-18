import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { UserLoginComponent } from './user-login/user-login.component';
import { ConfirmEmailChangeComponent } from './_components/confirm-email-change/confirm-email-change.component';
import { ConfirmEmailComponent } from './_components/confirm-email/confirm-email.component';
import { ConfirmMigrationEmailComponent } from './_components/confirm-migration-email/confirm-migration-email.component';
import { ConfirmResetPasswordComponent } from './_components/confirm-reset-password/confirm-reset-password.component';
import { RegisterComponent } from './_components/register/register.component';
import { ResetPasswordComponent } from './_components/reset-password/reset-password.component';

const routes: Routes = [
  {
    path: '',
    component: UserLoginComponent
  },
  {
    path: 'login',
    component: UserLoginComponent
  },
  {
      path: 'confirm-email',
      component: ConfirmEmailComponent,
  },
  {
      path: 'confirm-migration-email',
      component: ConfirmMigrationEmailComponent,
  },
  {
      path: 'confirm-email-update',
      component: ConfirmEmailChangeComponent,
  },
  {
    path: 'register',
    component: RegisterComponent,
  },
  {
    path: 'reset-password',
    component: ResetPasswordComponent
  },
  {
    path: 'confirm-reset-password',
    component: ConfirmResetPasswordComponent
  }
];


@NgModule({
    imports: [RouterModule.forChild(routes), ],
    exports: [RouterModule]
})
export class RegistrationRoutingModule { }
