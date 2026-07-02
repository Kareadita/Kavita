import {inject, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from "@jsverse/transloco";
import {RecommendationSource} from "../_models/kavitaplus/recommendation-source.enum";

@Pipe({
  name: 'recommendationSource',
})
export class RecommendationSourcePipe implements PipeTransform {

  private readonly transloco = inject(TranslocoService);

  transform(value: RecommendationSource): string {
    switch (value) {
      case RecommendationSource.UserBased:
        return this.transloco.translate('recommendation-source-pipe.user-based');
      case RecommendationSource.Similar:
        return this.transloco.translate('recommendation-source-pipe.similar');
      case RecommendationSource.Personalized:
        return this.transloco.translate('recommendation-source-pipe.personalized');
    }
  }

}
