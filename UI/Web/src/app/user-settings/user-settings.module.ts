import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeriesBookmarksComponent } from './series-bookmarks/series-bookmarks.component';
import { UserPreferencesComponent } from './user-preferences/user-preferences.component';
import { NgbAccordionModule, NgbNavModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { ReactiveFormsModule } from '@angular/forms';
import { NgxSliderModule } from '@angular-slider/ngx-slider';
import { UserSettingsRoutingModule } from './user-settings-routing.module';



@NgModule({
  declarations: [
    SeriesBookmarksComponent,
    UserPreferencesComponent
  ],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NgbAccordionModule,
    NgbNavModule,
    NgbTooltipModule,
    NgxSliderModule,
    UserSettingsRoutingModule,
  ]
})
export class UserSettingsModule { }
