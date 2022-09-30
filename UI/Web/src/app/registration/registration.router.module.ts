import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { ConfirmEmailChangeComponent } from './confirm-email-change/confirm-email-change.component';
import { ConfirmEmailComponent } from './confirm-email/confirm-email.component';
import { ConfirmMigrationEmailComponent } from './confirm-migration-email/confirm-migration-email.component';
import { ConfirmResetPasswordComponent } from './confirm-reset-password/confirm-reset-password.component';
import { RegisterComponent } from './register/register.component';
import { ResetPasswordComponent } from './reset-password/reset-password.component';
import { UserLoginComponent } from './user-login/user-login.component';

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
