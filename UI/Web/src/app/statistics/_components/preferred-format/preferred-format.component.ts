import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {StatisticsService} from "../../../_services/statistics.service";
import {TranslocoDirective} from "@jsverse/transloco";
import {MangaFormatPipe} from "../../../_pipes/manga-format.pipe";
import {PieChartModule} from "@swimlane/ngx-charts";
import {EChartsDirective, ECOption} from "../../../_directives/echarts.directive";
import {ThemeService} from "../../../_services/theme.service";
import {MangaFormat} from "../../../_models/manga-format";
import {StatsFilter} from "../../_models/stats-filter";

@Component({
  selector: 'app-preferred-format',
  imports: [
    TranslocoDirective,
    PieChartModule,
    EChartsDirective
  ],
  templateUrl: './preferred-format.component.html',
  styleUrl: './preferred-format.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PreferredFormatComponent {

  private readonly statsService = inject(StatisticsService);
  private readonly themeService = inject(ThemeService);

  userName = input.required<string>();
  userId = input.required<number>();
  filter = input.required<StatsFilter | undefined>();

  formatsResource = this.statsService
    .getPreferredFormatResource(() => this.filter(), () => this.userId());

  mostReadFormat = computed(() => {
    if (this.formatsResource.hasValue()) {
      const pipe = new MangaFormatPipe();

      const format = this.formatsResource.value()!.reduce((prev, cur) =>
        prev.count > cur.count ? prev : cur, {count: -1, value: MangaFormat.UNKNOWN}).value;

      return pipe.transform(format);
    }

    return null;
  })

  options = computed<ECOption>(() => {
    const data = this.formatsResource.hasValue() ?  this.formatsResource.value() : [];
    const pipe = new MangaFormatPipe();

    return {
      name: 'Format',
      legend: {
        top: '5%',
        left: 'center',
        textStyle: {
          color: this.themeService.getCssVariable('--body-text-color'),
        }
      },
      tooltip: {
        trigger: 'item'
      },
      series: [{
        type: 'pie',
        radius: ['40%', '70%'],
        center: ['50%', '70%'],
        startAngle: 180,
        endAngle: 360,
        color: this.themeService.chartsColourPalette(),
        data: (data || []).map(r => {
          return {
            value: r.count,
            name: pipe.transform(r.value)
          }
        }),
        label: {
          color: this.themeService.getCssVariable('--body-text-color'),
        }
      }],
    };
  });



}
