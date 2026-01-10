import {ChangeDetectionStrategy, Component, DestroyRef, inject, QueryList, ViewChildren} from '@angular/core';
import {FormControl, ReactiveFormsModule} from '@angular/forms';
import {BehaviorSubject, combineLatest, map, Observable} from 'rxjs';
import {StatisticsService} from 'src/app/_services/statistics.service';
import {compare, SortableHeader, SortEvent} from 'src/app/_single-module/table/_directives/sortable-header.directive';
import {PieDataItem} from '../../_models/pie-data-item';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {AsyncPipe, DecimalPipe} from '@angular/common';
import {TranslocoDirective} from "@jsverse/transloco";
import {ResponsiveTableComponent} from "../../../shared/_components/responsive-table/responsive-table.component";
import {
  DataTableColumnCellDirective,
  DataTableColumnDirective,
  DataTableColumnHeaderDirective,
  DatatableComponent
} from "@siemens/ngx-datatable";
import {StatsNoDataComponent} from "../../../common/stats-no-data/stats-no-data.component";

@Component({
    selector: 'app-publication-status-stats',
    templateUrl: './publication-status-stats.component.html',
    styleUrls: ['./publication-status-stats.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, AsyncPipe, DecimalPipe, TranslocoDirective, ResponsiveTableComponent, DatatableComponent, DataTableColumnDirective, DataTableColumnHeaderDirective, DataTableColumnCellDirective, StatsNoDataComponent]
})
export class PublicationStatusStatsComponent {
  private readonly statService = inject(StatisticsService);


  @ViewChildren(SortableHeader<PieDataItem>) headers!: QueryList<SortableHeader<PieDataItem>>;

  publicationStatues$!: Observable<Array<PieDataItem>>;

  currentSort = new BehaviorSubject<SortEvent<PieDataItem>>({column: 'value', direction: 'asc'});
  currentSort$: Observable<SortEvent<PieDataItem>> = this.currentSort.asObservable();

  view: [number, number] = [700, 400];

  private readonly destroyRef = inject(DestroyRef);

  formControl: FormControl = new FormControl(true, []);

  readonly trackByIdentity = (_: number, item: PieDataItem) => item.name + '_' + item.value;


  constructor() {
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
}
