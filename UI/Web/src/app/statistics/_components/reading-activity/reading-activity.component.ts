import {ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, OnInit, signal} from '@angular/core';
import {filter, Observable, of, shareReplay, tap} from 'rxjs';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {AsyncPipe} from '@angular/common';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {LineChartComponent} from "../../../shared/_charts/line-chart/line-chart.component";
import {MangaFormatPipe} from "../../../_pipes/manga-format.pipe";
import {MangaFormat} from "../../../_models/manga-format";
import {StatsFilter} from "../../_models/stats-filter";
import {AccountService} from "../../../_services/account.service";
import {StatsNoDataComponent} from "../../../common/stats-no-data/stats-no-data.component";
import {StatisticsService} from "../../../_services/statistics.service";
import {MemberService} from "../../../_services/member.service";
import {Member} from "../../../_models/auth/member";
import {disabled, form, FormField} from "@angular/forms/signals";
import {
  EnumOption,
  SettingSelectComponent
} from "../../../settings/_components/setting-enum-select/setting-select.component";

const dateOptions: Intl.DateTimeFormatOptions = { month: 'short', day: 'numeric' };

interface ProcessedChartData {
  axisLabels: string[];
  legendLabels: string[];
  data: number[][];
  hasData: boolean;
}

interface PagesReadOnADayCount {
  value: string | Date;
  count: number;
  format: MangaFormat;
}

@Component({
  selector: 'app-reading-activity',
  templateUrl: './reading-activity.component.html',
  styleUrls: ['./reading-activity.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, LineChartComponent, StatsNoDataComponent, FormField, SettingSelectComponent]
})
export class ReadingActivityComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  private readonly statService = inject(StatisticsService);
  private readonly memberService = inject(MemberService);
  private readonly accountService = inject(AccountService);
  private readonly mangaFormatPipe = new MangaFormatPipe();

  statsFilter = input.required<StatsFilter>();
  userId = input<number>(0);
  individualUserMode = input<boolean>(false);

  isAdmin = computed(() => this.accountService.hasAdminRole() ?? false);

  selectedUserId = signal<number>(0);
  formGroup = form(this.selectedUserId, path => {
    disabled(path, { when: () => !this.isAdmin()});
  });
  members = signal<Member[]>([]);
  memberOptions = computed<EnumOption<number>[]>(() => [...this.members().map(m => ({
    value: m.id,
    title: m.username
  })), { value: 0, title: translate('reading-activity.all-users') }]);

  private readCountsResource = this.statService.getReadCountResource(() => this.statsFilter(), () => this.selectedUserId());

  chartData = computed(() => {
    const data = this.readCountsResource.value();
    return this.transformData(data ?? []);
  });

  isLoading = computed(() => this.readCountsResource.isLoading());

  view: [number, number] = [0, 400];

  ngOnInit(): void {
    if (this.isAdmin()) {
      this.memberService.getMembers().pipe(
        takeUntilDestroyed(this.destroyRef),
        tap(members => this.members.set(members))
      ).subscribe();
    }

    this.selectedUserId.set(this.userId());
  }

  private transformData(data: PagesReadOnADayCount[]): ProcessedChartData {
    if (!data || data.length === 0) {
      return { axisLabels: [], legendLabels: [], data: [], hasData: false };
    }

    const uniqueDates = [...new Set(data.map(d => new Date(d.value).getTime()))]
      .sort((a, b) => a - b);

    const axisLabels = uniqueDates.map(ts =>
      new Date(ts).toLocaleDateString('en-US', dateOptions)
    );

    const presentFormats = [...new Set(data.map(d => d.format))].sort();
    const legendLabels = presentFormats.map(f => this.mangaFormatPipe.transform(f));

    const dateIndexMap = new Map<number, number>();
    uniqueDates.forEach((ts, idx) => dateIndexMap.set(ts, idx));

    const formatIndexMap = new Map<MangaFormat, number>();
    presentFormats.forEach((format, idx) => formatIndexMap.set(format, idx));

    const chartData: number[][] = presentFormats.map(() =>
      new Array(uniqueDates.length).fill(0)
    );

    for (const entry of data) {
      const dateTs = new Date(entry.value).getTime();
      const dateIdx = dateIndexMap.get(dateTs);
      const formatIdx = formatIndexMap.get(entry.format);

      if (dateIdx !== undefined && formatIdx !== undefined) {
        // Backend returns minutes, convert to hours on 2 decimal points
        chartData[formatIdx][dateIdx] = Math.round(entry.count / 60 * 100) / 100;
      }
    }

    return {
      axisLabels,
      legendLabels,
      data: chartData,
      hasData: true
    };
  }
}

