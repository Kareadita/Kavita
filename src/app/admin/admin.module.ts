import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminRoutingModule } from './admin-routing.module';
import { DashboardComponent } from './dashboard/dashboard.component';
import { NgbNavModule } from '@ng-bootstrap/ng-bootstrap';
import { ManageLibraryComponent } from './manage-library/manage-library.component';
import { ManageUsersComponent } from './manage-users/manage-users.component';



@NgModule({
  declarations: [ManageUsersComponent, DashboardComponent, ManageLibraryComponent],
  imports: [
    CommonModule,
    AdminRoutingModule,
    NgbNavModule
  ],
  providers: []
})
export class AdminModule { }
