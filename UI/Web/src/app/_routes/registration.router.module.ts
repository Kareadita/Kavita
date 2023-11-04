import { Routes } from '@angular/router';
import { UserLoginComponent } from '../registration/user-login/user-login.component';
import { ConfirmEmailChangeComponent } from '../registration/_components/confirm-email-change/confirm-email-change.component';
import { ConfirmEmailComponent } from '../registration/_components/confirm-email/confirm-email.component';
import { ConfirmMigrationEmailComponent } from '../registration/_components/confirm-migration-email/confirm-migration-email.component';
import { ConfirmResetPasswordComponent } from '../registration/_components/confirm-reset-password/confirm-reset-password.component';
import { RegisterComponent } from '../registration/_components/register/register.component';
import { ResetPasswordComponent } from '../registration/_components/reset-password/reset-password.component';

export const routes: Routes = [
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
