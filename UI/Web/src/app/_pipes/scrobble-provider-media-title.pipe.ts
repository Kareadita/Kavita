import {inject, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from '@jsverse/transloco';
import {ScrobbleProvider} from '../_services/scrobbling.service';

@Pipe({
  name: 'scrobbleProviderMediaTitle',
  standalone: true,
  pure: true,
})
export class ScrobbleProviderMediaTitlePipe implements PipeTransform {
  private readonly translocoService = inject(TranslocoService);

  transform(provider: ScrobbleProvider | null | undefined): string {
    switch (provider) {
      case ScrobbleProvider.AniList:
        return this.translocoService.translate('scrobble-provider-media-title-pipe.anilist-label');
      case ScrobbleProvider.Mal:
        return this.translocoService.translate('scrobble-provider-media-title-pipe.mal-label');
      case ScrobbleProvider.MangaBaka:
        return this.translocoService.translate('scrobble-provider-media-title-pipe.mangabaka-label');
      case ScrobbleProvider.Hardcover:
        return this.translocoService.translate('scrobble-provider-media-title-pipe.hardcover-label');
      case ScrobbleProvider.Cbr:
        return this.translocoService.translate('scrobble-provider-media-title-pipe.cbr-label');
      default:
        return '';
    }
  }
}
