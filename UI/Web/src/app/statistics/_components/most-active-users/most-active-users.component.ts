import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {StatisticsService} from "../../../_services/statistics.service";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {ActiveUserCardComponent} from "../active-user-card/active-user-card.component";

export type TimeFrame = 'week' | 'month' | 'year';

@Component({
  selector: 'app-most-active-users',
  imports: [
    TranslocoDirective,
    LoadingComponent,
    ActiveUserCardComponent
  ],
  templateUrl: './most-active-users.component.html',
  styleUrl: './most-active-users.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MostActiveUsersComponent {

  private readonly statsService = inject(StatisticsService);

  timeFrame = input<TimeFrame>('week');
  usersResource = this.statsService.getMostActiveUsers();

  timeFrameLabel = computed(() => {
    const labels: Record<TimeFrame, string> = {
      week: 'this week',
      month: 'this month',
      year: 'this year'
    };
    return labels[this.timeFrame()];
  });

}
