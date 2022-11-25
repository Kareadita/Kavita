import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { UserStatsComponent } from './_components/user-stats/user-stats.component';
import { TableModule } from '../_single-module/table/table.module';
import { UserStatsInfoCardsComponent } from './_components/user-stats-info-cards/user-stats-info-cards.component';
import { SharedModule } from '../shared/shared.module';
import { ServerStatsComponent } from './_components/server-stats/server-stats.component';
import { NgxChartsModule } from '@swimlane/ngx-charts';
import { ReleaseYearComponent } from './_components/release-year/release-year.component';


@NgModule({
  declarations: [
    UserStatsComponent,
    UserStatsInfoCardsComponent,
    ServerStatsComponent,
    ReleaseYearComponent
  ],
  imports: [
    CommonModule,
    TableModule,
    SharedModule,

    // Server only
    NgxChartsModule
  ],
  exports: [
    UserStatsComponent,
    ServerStatsComponent

  ]
})
export class StatisticsModule { }
