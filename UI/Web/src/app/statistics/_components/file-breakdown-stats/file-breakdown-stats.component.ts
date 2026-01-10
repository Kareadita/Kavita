import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  QueryList,
  TemplateRef,
  ViewChild,
  ViewChildren
} from '@angular/core';
import {ReactiveFormsModule} from '@angular/forms';
import {BehaviorSubject, combineLatest, map, Observable, shareReplay} from 'rxjs';
import {StatisticsService} from 'src/app/_services/statistics.service';
import {compare, SortableHeader, SortEvent} from 'src/app/_single-module/table/_directives/sortable-header.directive';
import {FileExtension, FileExtensionBreakdown} from '../../_models/file-breakdown';
import {PieDataItem} from '../../_models/pie-data-item';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {AsyncPipe} from '@angular/common';
import {TranslocoDirective, TranslocoService} from "@jsverse/transloco";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {NgxDatatableModule} from "@siemens/ngx-datatable";
import {MangaFormatPipe} from "../../../_pipes/manga-format.pipe";
import {BytesPipe} from "../../../_pipes/bytes.pipe";
import {CompactNumberPipe} from "../../../_pipes/compact-number.pipe";
import {ResponsiveTableComponent} from "../../../shared/_components/responsive-table/responsive-table.component";
import {StatsNoDataComponent} from "../../../common/stats-no-data/stats-no-data.component";

@Component({
  selector: 'app-file-breakdown-stats',
  templateUrl: './file-breakdown-stats.component.html',
  styleUrls: ['./file-breakdown-stats.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgbTooltip, ReactiveFormsModule, AsyncPipe, TranslocoDirective, NgxDatatableModule, MangaFormatPipe, BytesPipe, CompactNumberPipe, ResponsiveTableComponent, StatsNoDataComponent]
})
export class FileBreakdownStatsComponent {

  private readonly destroyRef = inject(DestroyRef);
  private readonly cdRef = inject(ChangeDetectorRef);

  @ViewChildren(SortableHeader<PieDataItem>) headers!: QueryList<SortableHeader<PieDataItem>>;
  @ViewChild('modalTable') modalTable!: TemplateRef<any>;

  rawData$!: Observable<FileExtensionBreakdown>;
  files$!: Observable<Array<FileExtension>>;
  vizData2$!: Observable<Array<PieDataItem>>;

  currentSort = new BehaviorSubject<SortEvent<FileExtension>>({column: 'extension', direction: 'asc'});
  currentSort$: Observable<SortEvent<FileExtension>> = this.currentSort.asObservable();

  view: [number, number] = [700, 400];

  downloadInProgress: {[key: string]: boolean}  = {};

  private readonly statService = inject(StatisticsService);
  private readonly translocoService = inject(TranslocoService);

  trackByExtension = (_: number, item: FileExtension) => item.extension + '_' + item.totalFiles;

  constructor() {
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
      return {name: d.extension || this.translocoService.translate('file-breakdown-stats.not-classified'), value: d.totalFiles, extra: d.totalSize};
    })));
  }


  export(format: string) {
    this.downloadInProgress[format] = true;
    this.cdRef.markForCheck();

    this.statService.downloadFileBreakdown(format)
      .subscribe(() => {
        this.downloadInProgress[format] = false;
        this.cdRef.markForCheck();
      });
  }
}
