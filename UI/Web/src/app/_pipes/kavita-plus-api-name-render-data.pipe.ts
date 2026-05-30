import {inject, Pipe, PipeTransform} from '@angular/core';
import {KavitaPlusApiName} from "../_models/kavitaplus/kavita-plus-api-name.enum";
import {TranslocoService} from "@jsverse/transloco";

@Pipe({
  name: 'kavitaPlusApiNameRenderData',
})
export class KavitaPlusApiNameRenderDataPipe implements PipeTransform {

  private readonly transloco = inject(TranslocoService);

  transform(value: KavitaPlusApiName): {title: string, description: string, icon: string} {
    switch (value) {
      case KavitaPlusApiName.CoverRequests:
        return {title: this.t('cover-requests-title'), description: this.t('cover-requests-description'), icon: 'fa-solid fa-image'};
      case KavitaPlusApiName.MetadataSync:
        return {title: this.t('metadata-sync-title'), description: this.t('metadata-sync-description'), icon: 'fa-solid fa-database'};
      case KavitaPlusApiName.SeriesMatched:
        return {title: this.t('series-matched-title'), description: this.t('series-matched-description'), icon: 'fa-solid fa-magnifying-glass'};
      case KavitaPlusApiName.Scrobbles:
        return {title: this.t('scrobbles-title'), description: this.t('scrobbles-description'), icon: 'fa-solid fa-paper-plane'};
      case KavitaPlusApiName.MalStackImport:
        return {title: this.t('mal-stack-import-title'), description: this.t('mal-stack-import-description'), icon: 'fa-solid fa-layer-group'};
      case KavitaPlusApiName.WantToRead:
        return {title: this.t('want-to-read-title'), description: this.t('want-to-read-description'), icon: 'fa-solid fa-bookmark'};
      case KavitaPlusApiName.Recommendations:
        return {title: this.t('recommendations-title'), description: this.t('recommendations-description'), icon: 'fa-solid fa-wand-magic-sparkles'};
      case KavitaPlusApiName.Reviews:
        return {title: this.t('reviews-title'), description: this.t('reviews-description'), icon: 'fa-solid fa-pen-fancy'};
    }
  }

  private t(key: string) {
    return this.transloco.translate('kavita-plus-api-name-title-desc-pipe.' + key);
  }

}
