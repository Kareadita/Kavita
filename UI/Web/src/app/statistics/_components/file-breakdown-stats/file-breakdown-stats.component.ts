import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  OnDestroy,
  QueryList,
  ViewChildren
} from '@angular/core';
import { FormControl } from '@angular/forms';
import { LegendPosition } from '@swimlane/ngx-charts';
import { Observable, Subject, BehaviorSubject, combineLatest, map, takeUntil, shareReplay } from 'rxjs';
import { StatisticsService } from 'src/app/_services/statistics.service';
import { SortableHeader, SortEvent, compare } from 'src/app/_single-module/table/_directives/sortable-header.directive';
import { FileExtension, FileExtensionBreakdown } from '../../_models/file-breakdown';
import { PieDataItem } from '../../_models/pie-data-item';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

export interface StackedBarChartDataItem {
  name: string,
  series: Array<PieDataItem>;
}

@Component({
  selector: 'app-file-breakdown-stats',
  templateUrl: './file-breakdown-stats.component.html',
  styleUrls: ['./file-breakdown-stats.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FileBreakdownStatsComponent {

  @ViewChildren(SortableHeader<PieDataItem>) headers!: QueryList<SortableHeader<PieDataItem>>;

  rawData$!: Observable<FileExtensionBreakdown>;
  files$!: Observable<Array<FileExtension>>;
  vizData$!: Observable<Array<StackedBarChartDataItem>>;
  vizData2$!: Observable<Array<PieDataItem>>;

  currentSort = new BehaviorSubject<SortEvent<FileExtension>>({column: 'extension', direction: 'asc'});
  currentSort$: Observable<SortEvent<FileExtension>> = this.currentSort.asObservable();

  private readonly destroyRef = inject(DestroyRef);

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
    this.rawData$ = this.statService.getFileBreakdown().pipe(takeUntilDestroyed(this.destroyRef), shareReplay());

    this.files$ = combineLatest([this.currentSort$, this.rawData$]).pipe(
      map(([sortConfig, data]) => {
        return {sortConfig, fileBreakdown: data.fileBreakdown};
      }),
      map(({ sortConfig, fileBreakdown }) => {
        return (sortConfig.column) ? fileBreakdown.sort((a: FileExtension, b: FileExtension) => {
          if (sortConfig.column === '') return 0;
          const res = compare(a[sortConfig.column], b[sortConfig.column]);
          return sortConfig.direction === 'asc' ? res : -res;
        }) : fileBreakdown;
      }),
      takeUntilDestroyed(this.destroyRef)
    );


    this.vizData2$ = this.files$.pipe(takeUntilDestroyed(this.destroyRef), map(data => data.map(d => {
      return {name: d.extension || 'Not Categorized', value: d.totalFiles, extra: d.totalSize};
    })));
  }

  onSort(evt: SortEvent<FileExtension>) {
    this.currentSort.next(evt);

    // Must clear out headers here
    this.headers.forEach((header) => {
      if (header.sortable !== evt.column) {
        header.direction = '';
      }
    });
  }

}
