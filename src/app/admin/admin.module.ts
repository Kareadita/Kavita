import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminRoutingModule } from './admin-routing.module';
import { DashboardComponent } from './dashboard/dashboard.component';
import { NgbNavModule } from '@ng-bootstrap/ng-bootstrap';
import { ManageLibraryComponent } from './manage-library/manage-library.component';
import { ManageUsersComponent } from './manage-users/manage-users.component';
import { LibraryEditorModalComponent } from './_modals/library-editor-modal/library-editor-modal.component';
import { SharedModule } from '../shared/shared.module';
import { LibraryAccessModalComponent } from './_modals/library-access-modal/library-access-modal.component';
import { DirectoryPickerComponent } from './_modals/directory-picker/directory-picker.component';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ResetPasswordModalComponent } from './_modals/reset-password-modal/reset-password-modal.component';




@NgModule({
  declarations: [
    ManageUsersComponent,
    DashboardComponent,
    ManageLibraryComponent,
    LibraryEditorModalComponent,
    LibraryAccessModalComponent,
    DirectoryPickerComponent,
    ResetPasswordModalComponent
  ],
  imports: [
    CommonModule,
    AdminRoutingModule,
    ReactiveFormsModule,
    FormsModule,
    NgbNavModule,
    SharedModule,
  ],
  providers: []
})
export class AdminModule { }
