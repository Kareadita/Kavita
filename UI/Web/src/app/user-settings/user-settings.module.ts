import { NgModule } from '@angular/core';
import {CommonModule, NgOptimizedImage} from '@angular/common';
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
import { LicenseComponent } from './user-license/license.component';
import { ManageKavitaPlusComponent } from './user-kavitaplus/manage-kavita-plus.component';
import {UserScrobbleHistoryComponent} from "../_single-module/user-scrobble-history/user-scrobble-history.component";
import { UserHoldsComponent } from "./user-holds/user-holds.component";
import {SharedModule} from "../shared/shared.module";


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
    LicenseComponent,
    ManageKavitaPlusComponent,
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
        UserHoldsComponent,
        SharedModule,
        NgOptimizedImage,
    ],
  exports: [
    SiteThemeProviderPipe,
    ApiKeyComponent,
    RestrictionSelectorComponent,
    ManageKavitaPlusComponent
  ]
})
export class UserSettingsModule { }
