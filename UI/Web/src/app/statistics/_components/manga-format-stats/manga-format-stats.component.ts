import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, QueryList, ViewChildren } from '@angular/core';
import { FormControl } from '@angular/forms';
import { LegendPosition } from '@swimlane/ngx-charts';
import { Observable, Subject, BehaviorSubject, combineLatest, map, takeUntil } from 'rxjs';
import { StatisticsService } from 'src/app/_services/statistics.service';
import { compare, SortableHeader, SortEvent } from 'src/app/_single-module/table/_directives/sortable-header.directive';
import { PieDataItem } from '../../_models/pie-data-item';

@Component({
  selector: 'app-manga-format-stats',
  templateUrl: './manga-format-stats.component.html',
  styleUrls: ['./manga-format-stats.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MangaFormatStatsComponent implements OnInit, OnDestroy {

  @ViewChildren(SortableHeader<PieDataItem>) headers!: QueryList<SortableHeader<PieDataItem>>;

  formats$!: Observable<Array<PieDataItem>>;
  private readonly onDestroy = new Subject<void>();
  
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
      takeUntil(this.onDestroy)
    );
  }

  ngOnInit(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
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
