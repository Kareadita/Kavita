import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminRoutingModule } from './admin-routing.module';
import { DashboardComponent } from './dashboard/dashboard.component';
import { NgbNavModule } from '@ng-bootstrap/ng-bootstrap';
import { ManageLibraryComponent } from './manage-library/manage-library.component';
import { ManageUsersComponent } from './manage-users/manage-users.component';
import { LibraryEditorModalComponent } from './_modals/library-editor-modal/library-editor-modal.component';
import { SharedModule } from '../shared/shared.module';



@NgModule({
  declarations: [ManageUsersComponent, DashboardComponent, ManageLibraryComponent, LibraryEditorModalComponent],
  imports: [
    CommonModule,
    AdminRoutingModule,
    NgbNavModule,
    SharedModule
  ],
  providers: []
})
export class AdminModule { }
