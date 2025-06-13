import {Pipe, PipeTransform} from '@angular/core';
import {FilterField} from "../_models/metadata/v2/filter-field";
import {translate} from "@jsverse/transloco";

/**
 * Responsible for taking a filter field and value (as a string) and translating into a "Browse X" heading for All Series page
 * Example: Genre & "Action" -> Browse Action
 * Example: Artist & "Joe Shmo" -> Browse Joe Shmo Works
 */
@Pipe({
  name: 'browseTitle'
})
export class BrowseTitlePipe implements PipeTransform {

  transform(field: FilterField, value: string): string {
    switch (field) {
      case FilterField.PublicationStatus:
        return translate('browse-title-pipe.publication-status', {value});
      case FilterField.AgeRating:
        return translate('browse-title-pipe.age-rating', {value});
      case FilterField.UserRating:
        return translate('browse-title-pipe.user-rating', {value});
      case FilterField.Tags:
        return translate('browse-title-pipe.tag', {value});
      case FilterField.Translators:
        return translate('browse-title-pipe.translator', {value});
      case FilterField.Characters:
        return translate('browse-title-pipe.character', {value});
      case FilterField.Publisher:
        return translate('browse-title-pipe.publisher', {value});
      case FilterField.Editor:
        return translate('browse-title-pipe.editor', {value});
      case FilterField.CoverArtist:
        return translate('browse-title-pipe.artist', {value});
      case FilterField.Letterer:
        return translate('browse-title-pipe.letterer', {value});
      case FilterField.Colorist:
        return translate('browse-title-pipe.colorist', {value});
      case FilterField.Inker:
        return translate('browse-title-pipe.inker', {value});
      case FilterField.Penciller:
        return translate('browse-title-pipe.penciller', {value});
      case FilterField.Writers:
        return translate('browse-title-pipe.writer', {value});
      case FilterField.Genres:
        return translate('browse-title-pipe.genre', {value});
      case FilterField.Libraries:
        return translate('browse-title-pipe.library', {value});
      case FilterField.Formats:
        return translate('browse-title-pipe.format', {value});
      case FilterField.ReleaseYear:
        return translate('browse-title-pipe.release-year', {value});
      case FilterField.Imprint:
        return translate('browse-title-pipe.imprint', {value});
      case FilterField.Team:
        return translate('browse-title-pipe.team', {value});
      case FilterField.Location:
        return translate('browse-title-pipe.location', {value});

      // These have no natural links in the app to demand a richer title experience
      case FilterField.Languages:
      case FilterField.CollectionTags:
      case FilterField.ReadProgress:
      case FilterField.ReadTime:
      case FilterField.Path:
      case FilterField.FilePath:
      case FilterField.WantToRead:
      case FilterField.ReadingDate:
      case FilterField.AverageRating:
      case FilterField.ReadLast:
      case FilterField.Summary:
      case FilterField.SeriesName:
      default:
        return '';
    }
  }

}
