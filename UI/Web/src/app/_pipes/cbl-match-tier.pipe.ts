import {inject, Pipe, PipeTransform} from '@angular/core';
import {CblMatchTier} from '../_models/reading-list/cbl/cbl-match-tier';
import {TranslocoService} from '@jsverse/transloco';

@Pipe({
  name: 'cblMatchTier',
  standalone: true
})
export class CblMatchTierPipe implements PipeTransform {

  translocoService = inject(TranslocoService);

  transform(tier: CblMatchTier | null): string {
    if (tier === null || tier === undefined) return '';
    switch (tier) {
      case CblMatchTier.RemapRule:
        return this.translocoService.translate('cbl-match-tier-pipe.remap-rule');
      case CblMatchTier.ExternalId:
        return this.translocoService.translate('cbl-match-tier-pipe.external-id');
      case CblMatchTier.ExactName:
        return this.translocoService.translate('cbl-match-tier-pipe.exact-name');
        case CblMatchTier.ComicVineNaming:
        return this.translocoService.translate('cbl-match-tier-pipe.comicvine-naming');
      case CblMatchTier.ArticleStripped:
        return this.translocoService.translate('cbl-match-tier-pipe.article-stripped');
      case CblMatchTier.ReprintStripped:
        return this.translocoService.translate('cbl-match-tier-pipe.reprint-stripped');
      case CblMatchTier.AlternateSeries:
        return this.translocoService.translate('cbl-match-tier-pipe.alternate-series');
      case CblMatchTier.UserDecision:
        return this.translocoService.translate('cbl-match-tier-pipe.user-decision');
      case CblMatchTier.Unmatched:
        return this.translocoService.translate('cbl-match-tier-pipe.unmatched');
      default:
        return '';
    }
  }
}
