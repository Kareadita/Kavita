import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { AdminGuard } from '../_guards/admin.guard';
import { UsersComponent } from './users/users.component';

const routes: Routes = [
  {path: '**', component: UsersComponent, pathMatch: 'full'},
  {
    runGuardsAndResolvers: 'always',
    canActivate: [AdminGuard],
    children: [
      {path: '/users', component: UsersComponent}
    ]
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class AdminRoutingModule { }
