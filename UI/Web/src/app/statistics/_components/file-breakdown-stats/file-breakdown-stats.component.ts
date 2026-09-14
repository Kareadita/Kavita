import {ChangeDetectionStrategy, Component, inject, signal,} from '@angular/core';
import {FileExtension} from '../../_models/file-breakdown';
import {TranslocoDirective} from "@jsverse/transloco";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {NgxDatatableModule} from "@siemens/ngx-datatable";
import {MangaFormatPipe} from "../../../_pipes/manga-format.pipe";
import {BytesPipe} from "../../../_pipes/bytes.pipe";
import {CompactNumberPipe} from "../../../_pipes/compact-number.pipe";
import {ResponsiveTableComponent} from "../../../shared/_components/responsive-table/responsive-table.component";
import {StatsNoDataComponent} from "../../../common/stats-no-data/stats-no-data.component";
import {StatisticsService} from "../../../_services/statistics.service";

@Component({
  selector: 'app-file-breakdown-stats',
  templateUrl: './file-breakdown-stats.component.html',
  styleUrls: ['./file-breakdown-stats.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgbTooltip, TranslocoDirective, NgxDatatableModule, MangaFormatPipe, BytesPipe,
    CompactNumberPipe, ResponsiveTableComponent, StatsNoDataComponent]
})
export class FileBreakdownStatsComponent {

  private readonly statService = inject(StatisticsService);

  protected files = signal<FileExtension[]>([]);
  protected totalSize = signal<number>(0);
  protected downloadInProgress = signal<Record<string, boolean>>({});

  view: [number, number] = [700, 400];

  protected readonly trackByExtension = (_: number, item: FileExtension) => item.extension + '_' + item.totalFiles;

  constructor() {
    this.statService.getFileBreakdown().subscribe(res => {
      // Using sort props breaks the table for some users; https://github.com/Kareadita/Kavita/issues/4365
      this.files.set(res.fileBreakdown.sort((a, b) => b.totalFiles - a.totalFiles));
      this.totalSize.set(res.totalFileSize);
    });
  }

  export(format: string) {
    this.downloadInProgress.update(x => ({...x, [format]: true}));

    this.statService.downloadFileBreakdown(format)
      .subscribe(() => {
        this.downloadInProgress.update(x => ({...x, [format]: false}));
      });
  }
}
