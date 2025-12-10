import {ChangeDetectionStrategy, Component, effect, inject, input, model} from '@angular/core';
import {StatisticsService} from "../../../_services/statistics.service";
import {StatsFilter} from "../../../statistics/_models/stats-filter";
import {TranslocoDirective} from "@jsverse/transloco";
import {DecimalPipe} from "@angular/common";
import {IconAndTitleComponent} from "../../../shared/icon-and-title/icon-and-title.component";

export interface ProfileStatBar {
  booksRead: number;
  comicsRead: number;
  pagesRead: number;
  wordsRead: number;
  authorsRead: number;
  reviews: number;
  ratings: number;
}

@Component({
  selector: 'app-profile-stat-bar',
  imports: [
    TranslocoDirective,
    DecimalPipe,
    IconAndTitleComponent
  ],
  templateUrl: './profile-stat-bar.component.html',
  styleUrl: './profile-stat-bar.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProfileStatBarComponent {
  private readonly statsService = inject(StatisticsService);

  userId = input.required<number>();
  year = input.required<number>();
  filter = input.required<StatsFilter>();

  data = model<ProfileStatBar>();
  dataResource = this.statsService.getUserOverallStats(() => this.filter(), () => this.userId());

  constructor() {


  }

}
