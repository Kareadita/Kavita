import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  QueryList,
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
import { NgIf, NgFor, AsyncPipe, DecimalPipe } from '@angular/common';
import { NgbTooltip } from '@ng-bootstrap/ng-bootstrap';
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
    selector: 'app-manga-format-stats',
    templateUrl: './manga-format-stats.component.html',
    styleUrls: ['./manga-format-stats.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [NgbTooltip, ReactiveFormsModule, NgIf, PieChartModule, SortableHeader_1, NgFor, AsyncPipe, DecimalPipe, TranslocoDirective]
})
export class MangaFormatStatsComponent {

  @ViewChildren(SortableHeader<PieDataItem>) headers!: QueryList<SortableHeader<PieDataItem>>;
  private readonly destroyRef = inject(DestroyRef);

  formats$!: Observable<Array<PieDataItem>>;

  currentSort = new BehaviorSubject<SortEvent<PieDataItem>>({column: 'value', direction: 'asc'});
  currentSort$: Observable<SortEvent<PieDataItem>> = this.currentSort.asObservable();

  view: [number, number] = [700, 400];
  showLegend: boolean = true;
  showLabels: boolean = true;
  legendPosition: LegendPosition = LegendPosition.Right;

  formControl: FormControl = new FormControl(true, []);


  constructor(private statService: StatisticsService) {
    this.formats$ = combineLatest([this.currentSort$, this.statService.getMangaFormat()]).pipe(
      map(([sortConfig, data]) => {
        return (sortConfig.column) ? data.sort((a: PieDataItem, b: PieDataItem) => {
          if (sortConfig.column === '') return 0;
          const res = compare(a[sortConfig.column], b[sortConfig.column]);
          return sortConfig.direction === 'asc' ? res : -res;
        }) : data;
      }),
      takeUntilDestroyed(this.destroyRef)
    );
  }

  onSort(evt: SortEvent<PieDataItem>) {
    this.currentSort.next(evt);

    // Must clear out headers here
    this.headers.forEach((header) => {
      if (header.sortable !== evt.column) {
        header.direction = '';
      }
    });
  }

}
