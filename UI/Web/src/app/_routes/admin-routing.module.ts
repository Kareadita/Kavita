import { Routes } from '@angular/router';
import { AdminGuard } from '../_guards/admin.guard';
import { DashboardComponent } from '../admin/dashboard/dashboard.component';

export const routes: Routes = [
  {path: '**', component: DashboardComponent, pathMatch: 'full', canActivate: [AdminGuard]},
  {
    path: '',
    runGuardsAndResolvers: 'always',
    canActivate: [AdminGuard],
    children: [
      {path: 'dashboard', component: DashboardComponent},
    ]
  }
];

