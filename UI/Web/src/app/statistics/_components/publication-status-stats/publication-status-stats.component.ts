import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  OnDestroy,
  QueryList,
  ViewChildren
} from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { LegendPosition, PieChartModule } from '@swimlane/ngx-charts';
import { Observable, Subject, map, takeUntil, combineLatest, BehaviorSubject } from 'rxjs';
import { StatisticsService } from 'src/app/_services/statistics.service';
import { compare, SortableHeader, SortEvent } from 'src/app/_single-module/table/_directives/sortable-header.directive';
import { PieDataItem } from '../../_models/pie-data-item';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { SortableHeader as SortableHeader_1 } from '../../../_single-module/table/_directives/sortable-header.directive';
import { NgIf, NgFor, AsyncPipe, DecimalPipe } from '@angular/common';

@Component({
    selector: 'app-publication-status-stats',
    templateUrl: './publication-status-stats.component.html',
    styleUrls: ['./publication-status-stats.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [ReactiveFormsModule, NgIf, PieChartModule, SortableHeader_1, NgFor, AsyncPipe, DecimalPipe]
})
export class PublicationStatusStatsComponent {

  @ViewChildren(SortableHeader<PieDataItem>) headers!: QueryList<SortableHeader<PieDataItem>>;

  publicationStatues$!: Observable<Array<PieDataItem>>;

  currentSort = new BehaviorSubject<SortEvent<PieDataItem>>({column: 'value', direction: 'asc'});
  currentSort$: Observable<SortEvent<PieDataItem>> = this.currentSort.asObservable();

  view: [number, number] = [700, 400];
  gradient: boolean = true;
  showLegend: boolean = true;
  showLabels: boolean = true;
  isDoughnut: boolean = false;
  legendPosition: LegendPosition = LegendPosition.Right;
  colorScheme = {
    domain: ['#5AA454', '#A10A28', '#C7B42C', '#AAAAAA']
  };

  private readonly destroyRef = inject(DestroyRef);

  formControl: FormControl = new FormControl(true, []);


  constructor(private statService: StatisticsService) {
    this.publicationStatues$ = combineLatest([this.currentSort$, this.statService.getPublicationStatus()]).pipe(
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
