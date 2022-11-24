import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { UserStatsComponent } from './_components/user-stats/user-stats.component';



@NgModule({
  declarations: [
    UserStatsComponent
  ],
  imports: [
    CommonModule
  ],
  exports: [
    UserStatsComponent
  ]
})
export class StatisticsModule { }
