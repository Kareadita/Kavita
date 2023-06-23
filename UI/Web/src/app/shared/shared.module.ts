import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { NgbCollapseModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { ConfirmDialogComponent } from './confirm-dialog/confirm-dialog.component';
import { ReadMoreComponent } from './read-more/read-more.component';
import { RouterModule } from '@angular/router';
import { DrawerComponent } from './drawer/drawer.component';
import { TagBadgeComponent } from './tag-badge/tag-badge.component';
import { A11yClickDirective } from './a11y-click.directive';
import { SeriesFormatComponent } from './series-format/series-format.component';
import { UpdateNotificationModalComponent } from './update-notification/update-notification-modal.component';
import { CircularLoaderComponent } from './circular-loader/circular-loader.component';
import { NgCircleProgressModule } from 'ng-circle-progress';
import { PersonBadgeComponent } from './person-badge/person-badge.component';
import { BadgeExpanderComponent } from './badge-expander/badge-expander.component';
import { PipeModule } from '../pipe/pipe.module';
import { IconAndTitleComponent } from './icon-and-title/icon-and-title.component';
import { LoadingComponent } from './loading/loading.component';
import {ImageComponent} from "./image/image.component";

@NgModule({
  declarations: [
    ConfirmDialogComponent,
    DrawerComponent,
    TagBadgeComponent,
    A11yClickDirective,
    SeriesFormatComponent,
    UpdateNotificationModalComponent,
    CircularLoaderComponent,
    BadgeExpanderComponent,
    IconAndTitleComponent,
    LoadingComponent,
  ],
  imports: [
    CommonModule,
    RouterModule,
    ReactiveFormsModule,
    NgbCollapseModule,
    NgbTooltipModule, // TODO: Validate if we still need this
    PipeModule,
    NgCircleProgressModule.forRoot(),
  ],
  exports: [
    DrawerComponent, // Can be replaced with boostrap offscreen canvas (v5) (also used in book reader and series metadata filter)
    A11yClickDirective, // Used globally
    SeriesFormatComponent, // Used globally
    TagBadgeComponent, // Used globally
    CircularLoaderComponent, // Used in Cards and Series Detail

    BadgeExpanderComponent, // Used Series Detail/Metadata
    IconAndTitleComponent, // Used in Series Detail/Metadata

    LoadingComponent
  ],
})
export class SharedModule { }
