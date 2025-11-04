import {
  ChangeDetectionStrategy,
  Component,
  computed,
  CUSTOM_ELEMENTS_SCHEMA,
  inject,
  input,
  model
} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {ActivityGraphData} from "@hsablonniere/activity-graph";
import {StatisticsService} from "../../../_services/statistics.service";
import {DateTime} from "luxon";
import {AccountService} from "../../../_services/account.service";

@Component({
  selector: 'app-activity-graph',
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  imports: [
      TranslocoDirective
  ],
  templateUrl: './activity-graph.component.html',
  styleUrl: './activity-graph.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ActivityGraphComponent {

  private readonly statsService = inject(StatisticsService);
  private readonly accountService = inject(AccountService);

  year = input<number>(new Date().getFullYear());
  data = model<ActivityGraphData>({});

  startDate = computed(() => {
    return `${this.year()}-01-01`;
  });

  endDate = computed(() => {
    return `${this.year()}-12-31`;
  });

  constructor() {
    this.statsService.getReadingActivity(this.accountService.currentUserSignal()?.id || 0, this.year()).subscribe(res => {
      this.data.set(res);
    });
  }

}
