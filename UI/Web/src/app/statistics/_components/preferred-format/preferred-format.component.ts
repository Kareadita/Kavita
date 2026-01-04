import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {StatisticsService} from "../../../_services/statistics.service";
import {TranslocoDirective} from "@jsverse/transloco";
import {MangaFormatPipe} from "../../../_pipes/manga-format.pipe";
import {MangaFormat} from "../../../_models/manga-format";
import {StatsFilter} from "../../_models/stats-filter";
import {PieChartComponent} from "../../../shared/_charts/pie-chart/pie-chart.component";
import {StatCount} from "../../_models/stat-count";
import {StatsNoDataComponent} from "../../../common/stats-no-data/stats-no-data.component";

@Component({
  selector: 'app-preferred-format',
  imports: [
    TranslocoDirective,
    PieChartComponent,
    StatsNoDataComponent
  ],
  templateUrl: './preferred-format.component.html',
  styleUrl: './preferred-format.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PreferredFormatComponent {

  private readonly statsService = inject(StatisticsService);
  private readonly pipe = new MangaFormatPipe();

  userName = input.required<string>();
  userId = input.required<number>();
  filter = input.required<StatsFilter | undefined>();

  formatsResource = this.statsService
    .getPreferredFormatResource(() => this.filter(), () => this.userId());

  mostReadFormat = computed(() => {
    if (this.formatsResource.hasValue()) {

      const format = this.formatsResource.value()!.reduce((prev, cur) =>
        prev.count > cur.count ? prev : cur, {count: -1, value: MangaFormat.UNKNOWN}).value;

      if (format === MangaFormat.UNKNOWN) return null;

      return this.pipe.transform(format);
    }

    return null;
  });

  data = computed(() => this.formatsResource.hasValue() ?  this.formatsResource.value() : []);

  valueTransformer(v: StatCount<MangaFormat>) {
    return this.pipe.transform(v.value);
  }

}
