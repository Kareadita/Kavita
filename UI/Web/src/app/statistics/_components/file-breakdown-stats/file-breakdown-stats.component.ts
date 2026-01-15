import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit, signal,} from '@angular/core';
import {ReactiveFormsModule} from '@angular/forms';
import {tap} from 'rxjs';
import {StatisticsService} from 'src/app/_services/statistics.service';
import {FileExtension} from '../../_models/file-breakdown';
import {TranslocoDirective} from "@jsverse/transloco";
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
  imports: [NgbTooltip, ReactiveFormsModule, TranslocoDirective, NgxDatatableModule, MangaFormatPipe, BytesPipe, CompactNumberPipe, ResponsiveTableComponent, StatsNoDataComponent]
})
export class FileBreakdownStatsComponent implements OnInit {

  private readonly cdRef = inject(ChangeDetectorRef);

  files = signal<FileExtension[]>([]);
  totalSize = signal<number>(0);

  view: [number, number] = [700, 400];

  downloadInProgress: {[key: string]: boolean}  = {};

  private readonly statService = inject(StatisticsService);

  trackByExtension = (_: number, item: FileExtension) => item.extension + '_' + item.totalFiles;

  ngOnInit() {
    this.statService.getFileBreakdown().pipe(
      tap(res => {
        // Using sort props breaks the table for some users; https://github.com/Kareadita/Kavita/issues/4365
        this.files.set(res.fileBreakdown.sort((a, b) => b.totalFiles - a.totalFiles));
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
