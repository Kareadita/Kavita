import {inject, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from "@jsverse/transloco";
import {MediaErrorProducer} from "../admin/_models/media-error";

@Pipe({
  name: 'mediaErrorProducer',
})
export class MediaErrorProducerPipe implements PipeTransform {
  private readonly translocoService = inject(TranslocoService);

  transform(value: MediaErrorProducer): string {
    switch (value) {
      case MediaErrorProducer.BookService:
        return this.translocoService.translate('media-error-producer-pipe.book-service');
      case MediaErrorProducer.ArchiveService:
        return this.translocoService.translate('media-error-producer-pipe.archive-service');
      case MediaErrorProducer.Scanner:
        return this.translocoService.translate('media-error-producer-pipe.scanner');

    }
  }
}
