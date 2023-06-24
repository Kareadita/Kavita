import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminRoutingModule } from './admin-routing.module';
import { DashboardComponent } from './dashboard/dashboard.component';
import { NgbAccordionModule, NgbDropdownModule, NgbNavModule, NgbTooltipModule, NgbTypeaheadModule } from '@ng-bootstrap/ng-bootstrap';
import { ManageLibraryComponent } from './manage-library/manage-library.component';
import { ManageUsersComponent } from './manage-users/manage-users.component';
import { LibraryAccessModalComponent } from './_modals/library-access-modal/library-access-modal.component';
import { DirectoryPickerComponent } from './_modals/directory-picker/directory-picker.component';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ResetPasswordModalComponent } from './_modals/reset-password-modal/reset-password-modal.component';
import { ManageSettingsComponent } from './manage-settings/manage-settings.component';
import { ManageSystemComponent } from './manage-system/manage-system.component';
import { InviteUserComponent } from './invite-user/invite-user.component';
import { RoleSelectorComponent } from './role-selector/role-selector.component';
import { LibrarySelectorComponent } from './library-selector/library-selector.component';
import { EditUserComponent } from './edit-user/edit-user.component';
import { UserSettingsModule } from '../user-settings/user-settings.module';
import { ManageMediaSettingsComponent } from './manage-media-settings/manage-media-settings.component';
import { ManageEmailSettingsComponent } from './manage-email-settings/manage-email-settings.component';
import { ManageTasksSettingsComponent } from './manage-tasks-settings/manage-tasks-settings.component';
import { ManageLogsComponent } from './manage-logs/manage-logs.component';
import { VirtualScrollerModule } from '@iharbeck/ngx-virtual-scroller';
import { StatisticsModule } from '../statistics/statistics.module';
import { ManageAlertsComponent } from './manage-alerts/manage-alerts.component';
import {ManageScrobbleErrorsComponent} from "./manage-scrobble-errors/manage-scrobble-errors.component";
import {DefaultValuePipe} from "../pipe/default-value.pipe";
import {LibraryTypePipe} from "../pipe/library-type.pipe";
import {TimeAgoPipe} from "../pipe/time-ago.pipe";
import {SentenceCasePipe} from "../pipe/sentence-case.pipe";
import {FilterPipe} from "../pipe/filter.pipe";
import {TagBadgeComponent} from "../shared/tag-badge/tag-badge.component";
import {LoadingComponent} from "../shared/loading/loading.component";
import {
  SideNavCompanionBarComponent
} from "../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";
import {RouterModule} from "@angular/router";




@NgModule({
  declarations: [
    ManageUsersComponent,
    DashboardComponent,
    ManageLibraryComponent,
    LibraryAccessModalComponent,
    DirectoryPickerComponent,
    ResetPasswordModalComponent,
    ManageSettingsComponent,
    ManageSystemComponent,
    InviteUserComponent,
    RoleSelectorComponent,
    LibrarySelectorComponent,
    EditUserComponent,
    ManageMediaSettingsComponent,
    ManageEmailSettingsComponent,
    ManageTasksSettingsComponent,
    ManageLogsComponent,
    ManageAlertsComponent,
  ],
  imports: [
    CommonModule,
    AdminRoutingModule,
    ReactiveFormsModule,
    RouterModule,
    FormsModule,
    NgbNavModule,
    NgbTooltipModule,
    NgbTypeaheadModule, // Directory Picker
    NgbDropdownModule,
    NgbAccordionModule,
    UserSettingsModule, // API-key componet
    VirtualScrollerModule,

    StatisticsModule,
    ManageScrobbleErrorsComponent,
    DefaultValuePipe,
    LibraryTypePipe,
    TimeAgoPipe,
    SentenceCasePipe,
    FilterPipe,
    TagBadgeComponent,
    LoadingComponent,
    SideNavCompanionBarComponent
  ],
  providers: []
})
export class AdminModule { }
