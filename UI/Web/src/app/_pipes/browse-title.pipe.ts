import {Pipe, PipeTransform} from '@angular/core';
import {SeriesFilterField} from "../_models/metadata/v2/series-filter-field";
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

  transform(field: SeriesFilterField, value: string): string {
    switch (field) {
      case SeriesFilterField.PublicationStatus:
        return translate('browse-title-pipe.publication-status', {value});
      case SeriesFilterField.AgeRating:
        return translate('browse-title-pipe.age-rating', {value});
      case SeriesFilterField.UserRating:
        return translate('browse-title-pipe.user-rating', {value});
      case SeriesFilterField.Tags:
        return translate('browse-title-pipe.tag', {value});
      case SeriesFilterField.Translators:
        return translate('browse-title-pipe.translator', {value});
      case SeriesFilterField.Characters:
        return translate('browse-title-pipe.character', {value});
      case SeriesFilterField.Publisher:
        return translate('browse-title-pipe.publisher', {value});
      case SeriesFilterField.Editor:
        return translate('browse-title-pipe.editor', {value});
      case SeriesFilterField.CoverArtist:
        return translate('browse-title-pipe.artist', {value});
      case SeriesFilterField.Letterer:
        return translate('browse-title-pipe.letterer', {value});
      case SeriesFilterField.Colorist:
        return translate('browse-title-pipe.colorist', {value});
      case SeriesFilterField.Inker:
        return translate('browse-title-pipe.inker', {value});
      case SeriesFilterField.Penciller:
        return translate('browse-title-pipe.penciller', {value});
      case SeriesFilterField.Writers:
        return translate('browse-title-pipe.writer', {value});
      case SeriesFilterField.Genres:
        return translate('browse-title-pipe.genre', {value});
      case SeriesFilterField.Libraries:
        return translate('browse-title-pipe.library', {value});
      case SeriesFilterField.Formats:
        return translate('browse-title-pipe.format', {value});
      case SeriesFilterField.ReleaseYear:
        return translate('browse-title-pipe.release-year', {value});
      case SeriesFilterField.Imprint:
        return translate('browse-title-pipe.imprint', {value});
      case SeriesFilterField.Team:
        return translate('browse-title-pipe.team', {value});
      case SeriesFilterField.Location:
        return translate('browse-title-pipe.location', {value});

      // These have no natural links in the app to demand a richer title experience
      case SeriesFilterField.Languages:
      case SeriesFilterField.CollectionTags:
      case SeriesFilterField.ReadProgress:
      case SeriesFilterField.ReadTime:
      case SeriesFilterField.Path:
      case SeriesFilterField.FilePath:
      case SeriesFilterField.WantToRead:
      case SeriesFilterField.ReadingDate:
      case SeriesFilterField.AverageRating:
      case SeriesFilterField.ReadLast:
      case SeriesFilterField.Summary:
      case SeriesFilterField.SeriesName:
      case SeriesFilterField.FileSize:
      default:
        return '';
    }
  }

}
