import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { NgbCollapseModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { ConfirmDialogComponent } from './confirm-dialog/confirm-dialog.component';
import { RouterModule } from '@angular/router';
import { DrawerComponent } from './drawer/drawer.component';
import { TagBadgeComponent } from './tag-badge/tag-badge.component';
import { A11yClickDirective } from './a11y-click.directive';
import { SeriesFormatComponent } from './series-format/series-format.component';
import { NgCircleProgressModule } from 'ng-circle-progress';
import { LoadingComponent } from './loading/loading.component';
import {MangaFormatIconPipe} from "../pipe/manga-format-icon.pipe";
import {MangaFormatPipe} from "../pipe/manga-format.pipe";
import {SafeHtmlPipe} from "../pipe/safe-html.pipe";

@NgModule({
  declarations: [
    ConfirmDialogComponent,
    DrawerComponent,
    TagBadgeComponent,
    A11yClickDirective,
    SeriesFormatComponent,
    LoadingComponent,
  ],
  imports: [
    CommonModule,
    RouterModule,
    ReactiveFormsModule,
    NgbCollapseModule,
    NgbTooltipModule, // TODO: Validate if we still need this
    NgCircleProgressModule.forRoot(),
    MangaFormatIconPipe,
    MangaFormatPipe,
    SafeHtmlPipe,
  ],
  exports: [
    DrawerComponent, // Can be replaced with boostrap offscreen canvas (v5) (also used in book reader and series metadata filter)
    A11yClickDirective, // Used globally
    SeriesFormatComponent, // Used globally
    TagBadgeComponent, // Used globally


    LoadingComponent
  ],
})
export class SharedModule { }
