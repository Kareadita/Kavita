import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { UserPreferencesComponent } from './user-preferences/user-preferences.component';
import { NgbAccordionModule, NgbNavModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { ReactiveFormsModule } from '@angular/forms';
import { UserSettingsRoutingModule } from './user-settings-routing.module';
import { ApiKeyComponent } from './api-key/api-key.component';
import { PipeModule } from '../pipe/pipe.module';
import { SiteThemeProviderPipe } from './_pipes/site-theme-provider.pipe';
import { ThemeManagerComponent } from './theme-manager/theme-manager.component';
import { ColorPickerModule } from 'ngx-color-picker';
import { SidenavModule } from '../sidenav/sidenav.module';
import { ManageDevicesComponent } from './manage-devices/manage-devices.component';



@NgModule({
  declarations: [
    UserPreferencesComponent,
    ApiKeyComponent,
    ThemeManagerComponent,
    SiteThemeProviderPipe,
    ManageDevicesComponent,
  ],
  imports: [
    CommonModule,
    ReactiveFormsModule,

    NgbAccordionModule,
    NgbNavModule,
    NgbTooltipModule,

    ColorPickerModule, // User prefernces background color
    
    PipeModule,
    SidenavModule,

    UserSettingsRoutingModule,
  ],
  exports: [
    SiteThemeProviderPipe,
    ApiKeyComponent
  ]
})
export class UserSettingsModule { }
