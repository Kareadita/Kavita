import {inject, Pipe, PipeTransform} from '@angular/core';
import {MetadataFetchTrigger} from "../_models/kavitaplus/metadata-fetch-trigger.enum";
import {TranslocoService} from "@jsverse/transloco";

@Pipe({
  name: 'metadataFetchTriggerTitle',
})
export class MetadataFetchTriggerTitlePipe implements PipeTransform {
  private readonly translocoService = inject(TranslocoService);

  transform(value: MetadataFetchTrigger): string {
    switch (value) {
      case MetadataFetchTrigger.SeriesAdded:
        return this.translocoService.translate('metadata-fetch-trigger-title-pipe.series-added');
      case MetadataFetchTrigger.OnDemand:
        return this.translocoService.translate('metadata-fetch-trigger-title-pipe.on-demand');
      case MetadataFetchTrigger.ManualMatch:
        return this.translocoService.translate('metadata-fetch-trigger-title-pipe.manual-match');
      case MetadataFetchTrigger.ScheduledRefresh:
        return this.translocoService.translate('metadata-fetch-trigger-title-pipe.scheduled-refresh');
      default:
        return '';
    }
  }
}
