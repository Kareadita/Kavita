import { Pipe, PipeTransform } from '@angular/core';
import { FilterField } from '../_models/filter-field';

@Pipe({
  name: 'filterField'
})
export class FilterFieldPipe implements PipeTransform {

  transform(value: FilterField): string {
    switch (value) {
      case FilterField.AgeRating:
        return 'Age Rating';
      case FilterField.Characters:
        return 'Characters';
      case FilterField.CollectionTags:
        return 'Collection Tags';
      case FilterField.Colorist:
        return 'Colorist';
      case FilterField.CoverArtist:
        return 'Cover Artist';
      case FilterField.Editor:
        return 'Editor';
      case FilterField.Formats:
        return 'Formats';
      case FilterField.Genres:
        return 'Genres';
      case FilterField.Inker:
        return 'Inker';
      case FilterField.Languages:
        return 'Languages';
      case FilterField.Libraries:
        return 'Libraries';
      case FilterField.Letterer:
        return 'Letterer';
      case FilterField.PublicationStatus:
        return 'Publication Status';
      case FilterField.Penciller:
        return 'Penciller';
      case FilterField.Publisher:
        return 'Publisher';
      case FilterField.ReadProgress:
        return 'Read Progress';
      case FilterField.ReadTime:
        return 'Read Time';
      case FilterField.ReleaseYear:
        return 'Release Year';
      case FilterField.SeriesName:
        return 'Series Name';
      case FilterField.Summary:
        return 'Summary';
      case FilterField.Tags:
        return 'Tags';
      case FilterField.Translators:
        return 'Translators';
      case FilterField.UserRating:
        return 'User Rating';
      case FilterField.Writers:
        return 'Writers';
      default:
        throw new Error(`Invalid FilterField value: ${value}`);
    }
  }

}
