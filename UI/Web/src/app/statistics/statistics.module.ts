import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { UserStatsComponent } from './_components/user-stats/user-stats.component';
import { TableModule } from '../_single-module/table/table.module';
import { UserStatsInfoCardsComponent } from './_components/user-stats-info-cards/user-stats-info-cards.component';
import { SharedModule } from '../shared/shared.module';
import { ServerStatsComponent } from './_components/server-stats/server-stats.component';
import { NgxChartsModule } from '@swimlane/ngx-charts';
import { StatListComponent } from './_components/stat-list/stat-list.component';
import { NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { PublicationStatusStatsComponent } from './_components/publication-status-stats/publication-status-stats.component';
import { ReactiveFormsModule } from '@angular/forms';
import { MangaFormatStatsComponent } from './_components/manga-format-stats/manga-format-stats.component';
import { FileBreakdownStatsComponent } from './_components/file-breakdown-stats/file-breakdown-stats.component';
import { PipeModule } from '../pipe/pipe.module';
import { TopReadsByExtensionComponent } from './_components/top-reads-by-extension/top-reads-by-extension.component';
import { TopReadsByUserComponent } from './_components/top-reads-by-user/top-reads-by-user.component';


@NgModule({
  declarations: [
    UserStatsComponent,
    UserStatsInfoCardsComponent,
    ServerStatsComponent,
    StatListComponent,
    PublicationStatusStatsComponent,
    MangaFormatStatsComponent,
    FileBreakdownStatsComponent,
    TopReadsByExtensionComponent,
    TopReadsByUserComponent
  ],
  imports: [
    CommonModule,
    TableModule,
    SharedModule,
    NgbTooltipModule,
    ReactiveFormsModule,
    PipeModule,

    // Server only
    NgxChartsModule
  ],
  exports: [
    UserStatsComponent,
    ServerStatsComponent

  ]
})
export class StatisticsModule { }
