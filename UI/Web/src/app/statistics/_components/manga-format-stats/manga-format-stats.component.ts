import {ChangeDetectionStrategy, Component, DestroyRef, inject, signal} from '@angular/core';
import {ReactiveFormsModule} from '@angular/forms';
import {StatisticsService} from 'src/app/_services/statistics.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {SortableHeader as SortableHeader_1} from '../../../_single-module/table/_directives/sortable-header.directive';
import {TranslocoDirective} from "@jsverse/transloco";
import {PieChartComponent} from "../../../shared/_charts/pie-chart/pie-chart.component";
import {StatCount} from "../../_models/stat-count";
import {MangaFormatPipe} from "../../../_pipes/manga-format.pipe";
import {MangaFormat} from "../../../_models/manga-format";
import {CompactNumberPipe} from "../../../_pipes/compact-number.pipe";

// TODO: Not in use, remove?
@Component({
  selector: 'app-manga-format-stats',
  templateUrl: './manga-format-stats.component.html',
  styleUrls: ['./manga-format-stats.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [ReactiveFormsModule, SortableHeader_1, TranslocoDirective, PieChartComponent, MangaFormatPipe, CompactNumberPipe]
})
export class MangaFormatStatsComponent {

  private readonly destroyRef = inject(DestroyRef);
  private readonly statService = inject(StatisticsService);
  private readonly mangaFormatPipe = new MangaFormatPipe();

  readonly chartData = signal<StatCount<MangaFormat>[]>([]);

  formatTransformer = (item: StatCount<MangaFormat>): string => {
    return this.mangaFormatPipe.transform(item.value);
  };

  constructor() {
    this.statService.getMangaFormat().pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(data => this.chartData.set(data));
  }

}
