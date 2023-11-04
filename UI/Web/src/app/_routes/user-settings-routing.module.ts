import { Routes } from '@angular/router';
import { AuthGuard } from '../_guards/auth.guard';
import { UserPreferencesComponent } from '../user-settings/user-preferences/user-preferences.component';

export const routes: Routes = [
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
