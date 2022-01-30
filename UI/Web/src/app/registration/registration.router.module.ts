import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { ConfirmEmailComponent } from './confirm-email/confirm-email.component';
import { ConfirmMigrationEmailComponent } from './confirm-migration-email/confirm-migration-email.component';
import { RegisterComponent } from './register/register.component';

const routes: Routes = [
  {
      path: 'confirm-email',
      component: ConfirmEmailComponent,
  },
  {
      path: 'confirm-migration-email',
      component: ConfirmMigrationEmailComponent,
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
