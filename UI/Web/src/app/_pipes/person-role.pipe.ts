import {inject, Pipe, PipeTransform} from '@angular/core';
import { PersonRole } from '../_models/metadata/person';
import {translate, TranslocoService} from "@jsverse/transloco";

@Pipe({
  name: 'personRole',
  standalone: true
})
export class PersonRolePipe implements PipeTransform {

  transform(value: PersonRole): string {
    switch (value) {
      case PersonRole.Artist:
        return translate('person-role-pipe.artist');
      case PersonRole.Character:
        return translate('person-role-pipe.character');
      case PersonRole.Colorist:
        return translate('person-role-pipe.colorist');
      case PersonRole.CoverArtist:
        return translate('person-role-pipe.artist');
      case PersonRole.Editor:
        return translate('person-role-pipe.editor');
      case PersonRole.Inker:
        return translate('person-role-pipe.inker');
      case PersonRole.Letterer:
        return translate('person-role-pipe.letterer');
      case PersonRole.Penciller:
        return translate('person-role-pipe.penciller');
      case PersonRole.Publisher:
        return translate('person-role-pipe.publisher');
      case PersonRole.Imprint:
        return translate('person-role-pipe.imprint');
      case PersonRole.Writer:
        return translate('person-role-pipe.writer');
      case PersonRole.Team:
        return translate('person-role-pipe.team');
      case PersonRole.Location:
        return translate('person-role-pipe.location');
      case PersonRole.Translator:
        return translate('person-role-pipe.translator');
      case PersonRole.Other:
        return translate('person-role-pipe.other');
      default:
        return '';
    }
  }

}
