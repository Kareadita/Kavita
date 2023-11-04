import { Routes } from '@angular/router';
import { AuthGuard } from '../_guards/auth.guard';
import { DashboardComponent } from '../dashboard/_components/dashboard.component';


export const routes: Routes = [
  {
    path: '',
    runGuardsAndResolvers: 'always',
    canActivate: [AuthGuard],
    component: DashboardComponent,
  }
];
