import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { AdminGuard } from '../_guards/admin.guard';
import { DashboardComponent } from './dashboard/dashboard.component';
import { UsersComponent } from './users/users.component';

const routes: Routes = [
  {path: '**', component: DashboardComponent, pathMatch: 'full'},
  {
    runGuardsAndResolvers: 'always',
    canActivate: [AdminGuard],
    children: [
      {path: '/dashboard', component: DashboardComponent},
      {path: '/users', component: UsersComponent}
    ]
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class AdminRoutingModule { }
