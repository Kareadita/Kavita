import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminRoutingModule } from './admin-routing.module';
import { DashboardComponent } from './dashboard/dashboard.component';
import { NgbNavModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { ManageLibraryComponent } from './manage-library/manage-library.component';
import { ManageUsersComponent } from './manage-users/manage-users.component';
import { LibraryEditorModalComponent } from './_modals/library-editor-modal/library-editor-modal.component';
import { SharedModule } from '../shared/shared.module';
import { LibraryAccessModalComponent } from './_modals/library-access-modal/library-access-modal.component';
import { DirectoryPickerComponent } from './_modals/directory-picker/directory-picker.component';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ResetPasswordModalComponent } from './_modals/reset-password-modal/reset-password-modal.component';
import { ManageSettingsComponent } from './manage-settings/manage-settings.component';
import { FilterPipe } from './filter.pipe';
import { EditRbsModalComponent } from './_modals/edit-rbs-modal/edit-rbs-modal.component';




@NgModule({
  declarations: [
    ManageUsersComponent,
    DashboardComponent,
    ManageLibraryComponent,
    LibraryEditorModalComponent,
    LibraryAccessModalComponent,
    DirectoryPickerComponent,
    ResetPasswordModalComponent,
    ManageSettingsComponent,
    FilterPipe,
    EditRbsModalComponent
  ],
  imports: [
    CommonModule,
    AdminRoutingModule,
    ReactiveFormsModule,
    FormsModule,
    NgbNavModule,
    NgbTooltipModule,
    SharedModule,
  ],
  providers: []
})
export class AdminModule { }
