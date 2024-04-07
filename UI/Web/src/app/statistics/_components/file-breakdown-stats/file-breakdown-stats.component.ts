import {
  ChangeDetectionStrategy, ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  QueryList, TemplateRef, ViewChild,
  ViewChildren
} from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { PieChartModule } from '@swimlane/ngx-charts';
import {Observable, BehaviorSubject, combineLatest, map, shareReplay, switchMap} from 'rxjs';
import { StatisticsService } from 'src/app/_services/statistics.service';
import { SortableHeader, SortEvent, compare } from 'src/app/_single-module/table/_directives/sortable-header.directive';
import { FileExtension, FileExtensionBreakdown } from '../../_models/file-breakdown';
import { PieDataItem } from '../../_models/pie-data-item';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { MangaFormatPipe } from '../../../_pipes/manga-format.pipe';
import { BytesPipe } from '../../../_pipes/bytes.pipe';
import { SortableHeader as SortableHeader_1 } from '../../../_single-module/table/_directives/sortable-header.directive';
import { NgIf, NgFor, AsyncPipe, DecimalPipe } from '@angular/common';
import {NgbModal, NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {translate, TranslocoDirective, TranslocoService} from "@ngneat/transloco";
import {filter, tap} from "rxjs/operators";
import {GenericTableModalComponent} from "../_modals/generic-table-modal/generic-table-modal.component";

export interface StackedBarChartDataItem {
  name: string,
  series: Array<PieDataItem>;
}

@Component({
    selector: 'app-file-breakdown-stats',
    templateUrl: './file-breakdown-stats.component.html',
    styleUrls: ['./file-breakdown-stats.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [NgbTooltip, ReactiveFormsModule, NgIf, PieChartModule, SortableHeader_1, NgFor, AsyncPipe, DecimalPipe, BytesPipe, MangaFormatPipe, TranslocoDirective]
})
export class FileBreakdownStatsComponent {

  private readonly destroyRef = inject(DestroyRef);
  private readonly cdRef = inject(ChangeDetectorRef);

  @ViewChildren(SortableHeader<PieDataItem>) headers!: QueryList<SortableHeader<PieDataItem>>;
  @ViewChild('tablelayout') tableTemplate!: TemplateRef<any>;

  rawData$!: Observable<FileExtensionBreakdown>;
  files$!: Observable<Array<FileExtension>>;
  vizData2$!: Observable<Array<PieDataItem>>;

  currentSort = new BehaviorSubject<SortEvent<FileExtension>>({column: 'extension', direction: 'asc'});
  currentSort$: Observable<SortEvent<FileExtension>> = this.currentSort.asObservable();

  view: [number, number] = [700, 400];

  formControl: FormControl = new FormControl(true, []);

  private readonly statService = inject(StatisticsService);
  private readonly translocoService = inject(TranslocoService);
  private readonly ngbModal = inject(NgbModal);

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

    // TODO: See if you can figure this out
    // this.formControl.valueChanges.pipe(filter(v => !v), takeUntilDestroyed(this.destroyRef), switchMap(_ => {
    //   const ref = this.ngbModal.open(GenericTableModalComponent);
    //   ref.componentInstance.title = translate('file-breakdown-stats.format-title');
    //   ref.componentInstance.bodyTemplate = this.tableTemplate;
    //   return ref.dismissed;
    // }, tap(_ => {
    //   this.formControl.setValue(true);
    //   this.cdRef.markForCheck();
    // }))).subscribe();
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
