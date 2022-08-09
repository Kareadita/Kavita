import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { AuthGuard } from '../_guards/auth.guard';
import { UserPreferencesComponent } from './user-preferences/user-preferences.component';

const routes: Routes = [
    {path: '**', component: UserPreferencesComponent, pathMatch: 'full'},
    {
      path: '',
      runGuardsAndResolvers: 'always',
      canActivate: [AuthGuard],
      children: [
        {path: '', component: UserPreferencesComponent, pathMatch: 'full'},
      ]
    }
];


@NgModule({
  imports: [RouterModule.forChild(routes), ],
  exports: [RouterModule]
})
export class UserSettingsRoutingModule { }
