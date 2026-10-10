import {inject, Pipe, PipeTransform} from '@angular/core';
import {MediaErrorReason} from "../admin/_models/media-error";
import {TranslocoService} from "@jsverse/transloco";

@Pipe({
  name: 'mediaErrorReason',
})
export class MediaErrorReasonPipe implements PipeTransform {
  private readonly translocoService = inject(TranslocoService);

  transform(value: MediaErrorReason): string {
    switch (value) {
      case MediaErrorReason.Unknown:
        return this.t('unknown');
      case MediaErrorReason.ParseFailed:
        return this.t('parse-failed');
      case MediaErrorReason.NoSeriesName:
        return this.t('no-series-name');
      case MediaErrorReason.UnreadableArchive:
        return this.t('unreadable-archive');
      case MediaErrorReason.CorruptEpub:
        return this.t('corrupt-epub');
      case MediaErrorReason.NoPages:
        return this.t('no-pages');
      case MediaErrorReason.CoverFailed:
        return this.t('cover-failed');
      case MediaErrorReason.WordCountFailed:
        return this.t('word-count-failed');
      case MediaErrorReason.IoError:
        return this.t('io-error');
      case MediaErrorReason.MetadataUnreadable:
        return this.t('metadata-unreadable');
      case MediaErrorReason.EpubNotStrict:
        return this.t('epub-not-strict');
    }
  }

  private t(key: string) {
    return this.translocoService.translate('media-error-reason-pipe.' + key);
  }
}
