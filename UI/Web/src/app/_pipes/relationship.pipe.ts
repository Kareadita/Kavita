import {inject, Pipe, PipeTransform} from '@angular/core';
import { RelationKind } from '../_models/series-detail/relation-kind';
import {TranslocoService} from "@jsverse/transloco";

@Pipe({
  name: 'relationship',
  standalone: true
})
export class RelationshipPipe implements PipeTransform {

  translocoService = inject(TranslocoService);

  transform(relationship: RelationKind | undefined): string {
    if (relationship === undefined) return '';
    switch (relationship) {
      case RelationKind.Adaptation:
        return this.translocoService.translate('relationship-pipe.adaptation');
      case RelationKind.AlternativeSetting:
        return this.translocoService.translate('relationship-pipe.alternative-setting');
      case RelationKind.AlternativeVersion:
        return this.translocoService.translate('relationship-pipe.alternative-version');
      case RelationKind.Character:
        return this.translocoService.translate('relationship-pipe.character');
      case RelationKind.Contains:
        return this.translocoService.translate('relationship-pipe.contains');
      case RelationKind.Doujinshi:
        return this.translocoService.translate('relationship-pipe.doujinshi');
      case RelationKind.Other:
        return this.translocoService.translate('relationship-pipe.other');
      case RelationKind.Prequel:
        return this.translocoService.translate('relationship-pipe.prequel');
      case RelationKind.Sequel:
        return this.translocoService.translate('relationship-pipe.sequel');
      case RelationKind.SideStory:
        return this.translocoService.translate('relationship-pipe.side-story');
      case RelationKind.SpinOff:
        return this.translocoService.translate('relationship-pipe.spin-off');
      case RelationKind.Parent:
        return this.translocoService.translate('relationship-pipe.parent');
      case RelationKind.Edition:
        return this.translocoService.translate('relationship-pipe.edition');
      case RelationKind.Annual:
        return this.translocoService.translate('relationship-pipe.annual');
      default:
        return '';
    }
  }

}
