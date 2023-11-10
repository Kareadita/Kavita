import { Routes } from '@angular/router';
import { UserPreferencesComponent } from '../user-settings/user-preferences/user-preferences.component';

export const routes: Routes = [
    {path: '', component: UserPreferencesComponent, pathMatch: 'full'},
];
