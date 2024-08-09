import { Routes } from '@angular/router';
import {SettingsComponent} from "../settings/_components/settings/settings.component";

export const routes: Routes = [
  {path: '', component: SettingsComponent, pathMatch: 'full'},
];
