import { NgModule } from '@angular/core';
import {CommonModule, NgOptimizedImage} from '@angular/common';
import { UserPreferencesComponent } from './user-preferences/user-preferences.component';
import {
  NgbAccordionBody,
  NgbAccordionButton, NgbAccordionCollapse,
  NgbAccordionDirective, NgbAccordionHeader, NgbAccordionItem,
  NgbCollapseModule,
  NgbNavModule,
  NgbTooltipModule
} from '@ng-bootstrap/ng-bootstrap';
import { ReactiveFormsModule } from '@angular/forms';
import { UserSettingsRoutingModule } from './user-settings-routing.module';
import { ApiKeyComponent } from './api-key/api-key.component';
import { SiteThemeProviderPipe } from './_pipes/site-theme-provider.pipe';
import { ThemeManagerComponent } from './theme-manager/theme-manager.component';
import { ColorPickerModule } from 'ngx-color-picker';
import { ManageDevicesComponent } from './manage-devices/manage-devices.component';
import { DevicePlatformPipe } from './_pipes/device-platform.pipe';
import { EditDeviceComponent } from './edit-device/edit-device.component';
import { ChangePasswordComponent } from './change-password/change-password.component';
import { ChangeEmailComponent } from './change-email/change-email.component';
import { ChangeAgeRestrictionComponent } from './change-age-restriction/change-age-restriction.component';
import { RestrictionSelectorComponent } from './restriction-selector/restriction-selector.component';

import { AnilistKeyComponent } from './anilist-key/anilist-key.component';
import {UserScrobbleHistoryComponent} from "../_single-module/user-scrobble-history/user-scrobble-history.component";
import { UserHoldsComponent } from "./user-holds/user-holds.component";
import {SentenceCasePipe} from "../pipe/sentence-case.pipe";
import {AgeRatingPipe} from "../pipe/age-rating.pipe";
import {LoadingComponent} from "../shared/loading/loading.component";
import {
  SideNavCompanionBarComponent
} from "../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";


@NgModule({
    imports: [
    CommonModule,
    ReactiveFormsModule,
    NgbNavModule,
    NgbTooltipModule,
    NgbCollapseModule,
    ColorPickerModule,
    UserSettingsRoutingModule,
    UserScrobbleHistoryComponent,
    UserHoldsComponent,
    NgOptimizedImage,
    SentenceCasePipe,
    AgeRatingPipe,
    LoadingComponent,
    SideNavCompanionBarComponent,
    NgbAccordionDirective,
    NgbAccordionItem,
    NgbAccordionHeader,
    NgbAccordionButton,
    NgbAccordionCollapse,
    NgbAccordionBody,
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
],
    exports: [
        SiteThemeProviderPipe,
        ApiKeyComponent,
        RestrictionSelectorComponent,
    ]
})
export class UserSettingsModule { }
