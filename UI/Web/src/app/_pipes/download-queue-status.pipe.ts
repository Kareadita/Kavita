import {inject, Pipe, PipeTransform} from '@angular/core';
import {DownloadQueueStatus} from "../shared/_models/download-queue-item";
import {TranslocoService} from "@jsverse/transloco";

@Pipe({
  name: 'downloadQueueStatus',
})
export class DownloadQueueStatusPipe implements PipeTransform {
  private readonly translocoService = inject(TranslocoService);

  transform(value: DownloadQueueStatus): string {
    return this.translocoService.translate('download-queue-status-pipe.' + value);
  }

}
