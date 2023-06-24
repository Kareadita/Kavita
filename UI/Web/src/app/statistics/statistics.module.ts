import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { UserStatsComponent } from './_components/user-stats/user-stats.component';
import { TableModule } from '../_single-module/table/table.module';
import { UserStatsInfoCardsComponent } from './_components/user-stats-info-cards/user-stats-info-cards.component';
import { ServerStatsComponent } from './_components/server-stats/server-stats.component';
import { NgxChartsModule } from '@swimlane/ngx-charts';
import { StatListComponent } from './_components/stat-list/stat-list.component';
import { NgbModalModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { PublicationStatusStatsComponent } from './_components/publication-status-stats/publication-status-stats.component';
import { ReactiveFormsModule } from '@angular/forms';
import { MangaFormatStatsComponent } from './_components/manga-format-stats/manga-format-stats.component';
import { FileBreakdownStatsComponent } from './_components/file-breakdown-stats/file-breakdown-stats.component';
import { TopReadersComponent } from './_components/top-readers/top-readers.component';
import { ReadingActivityComponent } from './_components/reading-activity/reading-activity.component';
import { GenericListModalComponent } from './_components/_modals/generic-list-modal/generic-list-modal.component';
import { DayBreakdownComponent } from './_components/day-breakdown/day-breakdown.component';
import { DayOfWeekPipe } from './_pipes/day-of-week.pipe';
import {IconAndTitleComponent} from "../shared/icon-and-title/icon-and-title.component";
import {ImageComponent} from "../shared/image/image.component";
import {CompactNumberPipe} from "../pipe/compact-number.pipe";
import {TimeDurationPipe} from "../pipe/time-duration.pipe";
import {TimeAgoPipe} from "../pipe/time-ago.pipe";
import {BytesPipe} from "../pipe/bytes.pipe";
import {MangaFormatPipe} from "../pipe/manga-format.pipe";
import {FilterPipe} from "../pipe/filter.pipe";



@NgModule({
  declarations: [
    UserStatsComponent,
    UserStatsInfoCardsComponent,
    ServerStatsComponent,
    StatListComponent,
    PublicationStatusStatsComponent,
    MangaFormatStatsComponent,
    FileBreakdownStatsComponent,
    TopReadersComponent,
    ReadingActivityComponent,
    GenericListModalComponent,
    DayBreakdownComponent,
    DayOfWeekPipe
  ],
  imports: [
    CommonModule,
    TableModule,
    NgbTooltipModule,
    NgbModalModule,
    ReactiveFormsModule,

    // Server only
    NgxChartsModule,
    IconAndTitleComponent,
    ImageComponent,
    CompactNumberPipe,
    TimeDurationPipe,
    TimeAgoPipe,
    BytesPipe,
    MangaFormatPipe,
    FilterPipe
  ],
  exports: [
    UserStatsComponent,
    ServerStatsComponent

  ]
})
export class StatisticsModule { }
