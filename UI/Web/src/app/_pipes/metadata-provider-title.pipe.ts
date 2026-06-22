import {inject, Pipe, PipeTransform} from '@angular/core';
import {MetadataProvider} from "../_models/kavitaplus/metadata-provider.enum";
import {TranslocoService} from "@jsverse/transloco";

@Pipe({
  name: 'metadataProviderTitle',
})
export class MetadataProviderTitlePipe implements PipeTransform {

  private readonly transloco = inject(TranslocoService);

  transform(value: MetadataProvider): string {
    switch (value) {
      case MetadataProvider.Hardcover:
        return this.transloco.translate('metadata-provider-title-pipe.hardcover');
      case MetadataProvider.Mangabaka:
        return this.transloco.translate('metadata-provider-title-pipe.mangabaka');
      case MetadataProvider.ComicBookRoundup:
        return this.transloco.translate('metadata-provider-title-pipe.cbr');
    }
  }

}
