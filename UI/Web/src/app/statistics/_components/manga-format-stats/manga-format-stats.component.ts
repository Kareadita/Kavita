import {ChangeDetectionStrategy, Component, DestroyRef, inject, signal} from '@angular/core';
import {ReactiveFormsModule} from '@angular/forms';
import {StatisticsService} from 'src/app/_services/statistics.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {TranslocoDirective} from "@jsverse/transloco";
import {PieChartComponent} from "../../../shared/_charts/pie-chart/pie-chart.component";
import {StatCount} from "../../_models/stat-count";
import {MangaFormatPipe} from "../../../_pipes/manga-format.pipe";
import {MangaFormat} from "../../../_models/manga-format";
import {CompactNumberPipe} from "../../../_pipes/compact-number.pipe";
import {ResponsiveTableComponent} from "../../../shared/_components/responsive-table/responsive-table.component";
import {DataTableColumnDirective, DatatableComponent} from "@siemens/ngx-datatable";

// TODO: Not in use, remove?
@Component({
  selector: 'app-manga-format-stats',
  templateUrl: './manga-format-stats.component.html',
  styleUrls: ['./manga-format-stats.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [ReactiveFormsModule, TranslocoDirective, PieChartComponent, MangaFormatPipe, CompactNumberPipe, ResponsiveTableComponent, DatatableComponent, DataTableColumnDirective]
})
export class MangaFormatStatsComponent {

  private readonly destroyRef = inject(DestroyRef);
  private readonly statService = inject(StatisticsService);
  private readonly mangaFormatPipe = new MangaFormatPipe();

  readonly chartData = signal<StatCount<MangaFormat>[]>([]);
  readonly trackByIdentity = (_: number, item: StatCount<MangaFormat>) => `${item.value}_${item.count}`;

  formatTransformer = (item: StatCount<MangaFormat>): string => {
    return this.mangaFormatPipe.transform(item.value);
  };

  constructor() {
    this.statService.getMangaFormat().pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(data => this.chartData.set(data));
  }

}
