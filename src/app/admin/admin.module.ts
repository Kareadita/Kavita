import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminRoutingModule } from './admin-routing.module';
import { UsersComponent } from './users/users.component';
import { ToastrModule } from 'ngx-toastr';
import { DashboardComponent } from './dashboard/dashboard.component';
import { NgbNavModule } from '@ng-bootstrap/ng-bootstrap';



@NgModule({
  declarations: [UsersComponent, DashboardComponent],
  imports: [
    CommonModule,
    AdminRoutingModule,
    NgbNavModule
  ],
  providers: []
})
export class AdminModule { }
