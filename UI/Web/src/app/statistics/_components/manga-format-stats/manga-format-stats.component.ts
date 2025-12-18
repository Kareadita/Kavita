import {
  ChangeDetectionStrategy,
  Component, computed,
  DestroyRef,
  inject,
  QueryList, signal,
  ViewChildren
} from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { LegendPosition, PieChartModule } from '@swimlane/ngx-charts';
import { Observable, BehaviorSubject, combineLatest, map } from 'rxjs';
import { StatisticsService } from 'src/app/_services/statistics.service';
import { compare, SortableHeader, SortEvent } from 'src/app/_single-module/table/_directives/sortable-header.directive';
import { PieDataItem } from '../../_models/pie-data-item';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { SortableHeader as SortableHeader_1 } from '../../../_single-module/table/_directives/sortable-header.directive';
import { AsyncPipe, DecimalPipe } from '@angular/common';
import { NgbTooltip } from '@ng-bootstrap/ng-bootstrap';
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
  imports: [ReactiveFormsModule, PieChartModule, SortableHeader_1, DecimalPipe, TranslocoDirective, PieChartComponent, MangaFormatPipe, CompactNumberPipe]
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
