import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject, OnInit,
  QueryList, signal,
  TemplateRef,
  ViewChild,
  ViewChildren
} from '@angular/core';
import {ReactiveFormsModule} from '@angular/forms';
import {BehaviorSubject, combineLatest, map, Observable, shareReplay, tap} from 'rxjs';
import {StatisticsService} from 'src/app/_services/statistics.service';
import {compare, SortableHeader, SortEvent} from 'src/app/_single-module/table/_directives/sortable-header.directive';
import {FileExtension, FileExtensionBreakdown} from '../../_models/file-breakdown';
import {PieDataItem} from '../../_models/pie-data-item';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {AsyncPipe} from '@angular/common';
import {TranslocoDirective, TranslocoService} from "@jsverse/transloco";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {NgxDatatableModule, SortDirection} from "@siemens/ngx-datatable";
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
  imports: [NgbTooltip, ReactiveFormsModule, TranslocoDirective, NgxDatatableModule, MangaFormatPipe, BytesPipe, CompactNumberPipe, ResponsiveTableComponent, StatsNoDataComponent]
})
export class FileBreakdownStatsComponent implements OnInit {

  private readonly cdRef = inject(ChangeDetectorRef);

  files = signal<FileExtension[]>([]);
  totalSize = signal<number>(0);

  view: [number, number] = [700, 400];

  downloadInProgress: {[key: string]: boolean}  = {};

  private readonly statService = inject(StatisticsService);
  private readonly translocoService = inject(TranslocoService);

  trackByExtension = (_: number, item: FileExtension) => item.extension + '_' + item.totalFiles;

  ngOnInit() {
    this.statService.getFileBreakdown().pipe(
      tap(res => {
        this.files.set(res.fileBreakdown);
        this.totalSize.set(res.totalFileSize);
      })
    ).subscribe();
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
