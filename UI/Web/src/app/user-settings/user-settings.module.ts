import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { UserPreferencesComponent } from './user-preferences/user-preferences.component';
import { NgbAccordionModule, NgbCollapseModule, NgbNavModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { ReactiveFormsModule } from '@angular/forms';
import { UserSettingsRoutingModule } from './user-settings-routing.module';
import { ApiKeyComponent } from './api-key/api-key.component';
import { PipeModule } from '../pipe/pipe.module';
import { SiteThemeProviderPipe } from './_pipes/site-theme-provider.pipe';
import { ThemeManagerComponent } from './theme-manager/theme-manager.component';
import { ColorPickerModule } from 'ngx-color-picker';
import { SidenavModule } from '../sidenav/sidenav.module';
import { ManageDevicesComponent } from './manage-devices/manage-devices.component';
import { DevicePlatformPipe } from './_pipes/device-platform.pipe';
import { EditDeviceComponent } from './edit-device/edit-device.component';
import { ChangePasswordComponent } from './change-password/change-password.component';
import { ChangeEmailComponent } from './change-email/change-email.component';
import { ChangeAgeRestrictionComponent } from './change-age-restriction/change-age-restriction.component';
import { RestrictionSelectorComponent } from './restriction-selector/restriction-selector.component';
import { StatisticsModule } from '../statistics/statistics.module';
import { AnilistKeyComponent } from './anilist-key/anilist-key.component';
import { UserLicenseComponent } from './user-license/user-license.component';
import { UserKavitaPlusComponent } from './user-kavitaplus/user-kavita-plus.component';
import {UserScrobbleHistoryComponent} from "../_single-module/user-scrobble-history/user-scrobble-history.component";


@NgModule({
  declarations: [
    UserPreferencesComponent,
    ApiKeyComponent,
    ThemeManagerComponent,
    SiteThemeProviderPipe,
    ManageDevicesComponent,
    DevicePlatformPipe,
    EditDeviceComponent,
    ChangePasswordComponent,
    ChangeEmailComponent,
    RestrictionSelectorComponent,
    ChangeAgeRestrictionComponent,
    AnilistKeyComponent,
    UserLicenseComponent,
    UserKavitaPlusComponent,
  ],
  imports: [
    CommonModule,
    ReactiveFormsModule,

    NgbAccordionModule,
    NgbNavModule,
    NgbTooltipModule,
    NgbCollapseModule,

    ColorPickerModule, // User prefernces background color

    StatisticsModule,

    PipeModule,
    SidenavModule,

    UserSettingsRoutingModule,
    UserScrobbleHistoryComponent,
  ],
  exports: [
    SiteThemeProviderPipe,
    ApiKeyComponent,
    RestrictionSelectorComponent
  ]
})
export class UserSettingsModule { }
