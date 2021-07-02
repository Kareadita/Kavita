import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { AdminGuard } from '../_guards/admin.guard';
import { DashboardComponent } from './dashboard/dashboard.component';

const routes: Routes = [
  {path: '**', component: DashboardComponent, pathMatch: 'full'},
  {
    runGuardsAndResolvers: 'always',
    canActivate: [AdminGuard],
    children: [
      {path: '/dashboard', component: DashboardComponent},
    ]
  }
];


@NgModule({
  imports: [RouterModule.forChild(routes), ],
  exports: [RouterModule]
})
export class AdminRoutingModule { }
