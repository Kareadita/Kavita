import {inject, Pipe, PipeTransform} from '@angular/core';
import { PersonRole } from '../_models/metadata/person';
import {TranslocoService} from "@ngneat/transloco";

@Pipe({
  name: 'personRole',
  standalone: true
})
export class PersonRolePipe implements PipeTransform {

  translocoService = inject(TranslocoService);
  transform(value: PersonRole): string {
    switch (value) {
      case PersonRole.Artist:
        return this.translocoService.translate('person-role-pipe.artist');
      case PersonRole.Character:
        return this.translocoService.translate('person-role-pipe.character');
      case PersonRole.Colorist:
        return this.translocoService.translate('person-role-pipe.colorist');
      case PersonRole.CoverArtist:
        return this.translocoService.translate('person-role-pipe.cover-artist');
      case PersonRole.Editor:
        return this.translocoService.translate('person-role-pipe.editor');
      case PersonRole.Inker:
        return this.translocoService.translate('person-role-pipe.inker');
      case PersonRole.Letterer:
        return this.translocoService.translate('person-role-pipe.letterer');
      case PersonRole.Penciller:
        return this.translocoService.translate('person-role-pipe.penciller');
      case PersonRole.Publisher:
        return this.translocoService.translate('person-role-pipe.publisher');
      case PersonRole.Writer:
        return this.translocoService.translate('person-role-pipe.writer');
      case PersonRole.Other:
        return this.translocoService.translate('person-role-pipe.other');
      default:
        return '';
    }
  }

}
