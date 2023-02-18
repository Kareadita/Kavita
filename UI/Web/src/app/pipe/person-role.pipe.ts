import { Pipe, PipeTransform } from '@angular/core';
import { PersonRole } from '../_models/metadata/person';

@Pipe({
  name: 'personRole'
})
export class PersonRolePipe implements PipeTransform {

  transform(value: PersonRole): string {
    switch (value) {
      case PersonRole.Artist: return 'Artist';
      case PersonRole.Character: return 'Character';
      case PersonRole.Colorist: return 'Colorist';
      case PersonRole.CoverArtist: return 'Cover Artist';
      case PersonRole.Editor: return 'Editor';
      case PersonRole.Inker: return 'Inker';
      case PersonRole.Letterer: return 'Letterer';
      case PersonRole.Penciller: return 'Penciller';
      case PersonRole.Publisher: return 'Publisher';
      case PersonRole.Writer: return 'Writer';
      case PersonRole.Other: return '';
      default: return '';
    }
  }

}
