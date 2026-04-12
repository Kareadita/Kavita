import {Pipe, PipeTransform} from '@angular/core';
import {SeriesFilterField} from 'src/app/_models/metadata/v2/series-filter-field';
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'filterField',
  standalone: true
})
export class FilterFieldPipe implements PipeTransform {

  transform(value: SeriesFilterField): string {
    switch (value) {
      case SeriesFilterField.AgeRating:
        return translate('filter-field-pipe.age-rating');
      case SeriesFilterField.Characters:
        return translate('filter-field-pipe.characters');
      case SeriesFilterField.CollectionTags:
        return translate('filter-field-pipe.collection-tags');
      case SeriesFilterField.Colorist:
        return translate('filter-field-pipe.colorist');
      case SeriesFilterField.CoverArtist:
        return translate('filter-field-pipe.cover-artist');
      case SeriesFilterField.Editor:
        return translate('filter-field-pipe.editor');
      case SeriesFilterField.Formats:
        return translate('filter-field-pipe.formats');
      case SeriesFilterField.Genres:
        return translate('filter-field-pipe.genres');
      case SeriesFilterField.Inker:
        return translate('filter-field-pipe.inker');
      case SeriesFilterField.Imprint:
        return translate('filter-field-pipe.imprint');
      case SeriesFilterField.Team:
        return translate('filter-field-pipe.team');
      case SeriesFilterField.Location:
        return translate('filter-field-pipe.location');
      case SeriesFilterField.Languages:
        return translate('filter-field-pipe.languages');
      case SeriesFilterField.Libraries:
        return translate('filter-field-pipe.libraries');
      case SeriesFilterField.Letterer:
        return translate('filter-field-pipe.letterer');
      case SeriesFilterField.PublicationStatus:
        return translate('filter-field-pipe.publication-status');
      case SeriesFilterField.Penciller:
        return translate('filter-field-pipe.penciller');
      case SeriesFilterField.Publisher:
        return translate('filter-field-pipe.publisher');
      case SeriesFilterField.ReadProgress:
        return translate('filter-field-pipe.read-progress');
      case SeriesFilterField.ReadTime:
        return translate('filter-field-pipe.read-time');
      case SeriesFilterField.ReleaseYear:
        return translate('filter-field-pipe.release-year');
      case SeriesFilterField.SeriesName:
        return translate('filter-field-pipe.series-name');
      case SeriesFilterField.Summary:
        return translate('filter-field-pipe.summary');
      case SeriesFilterField.Tags:
        return translate('filter-field-pipe.tags');
      case SeriesFilterField.Translators:
        return translate('filter-field-pipe.translators');
      case SeriesFilterField.UserRating:
        return translate('filter-field-pipe.user-rating');
      case SeriesFilterField.Writers:
        return translate('filter-field-pipe.writers');
      case SeriesFilterField.Path:
        return translate('filter-field-pipe.path');
      case SeriesFilterField.FilePath:
        return translate('filter-field-pipe.file-path');
      case SeriesFilterField.WantToRead:
        return translate('filter-field-pipe.want-to-read');
      case SeriesFilterField.ReadingDate:
        return translate('filter-field-pipe.read-date');
        case SeriesFilterField.ReadLast:
        return translate('filter-field-pipe.read-last');
      case SeriesFilterField.AverageRating:
        return translate('filter-field-pipe.average-rating');
      case SeriesFilterField.FileSize:
        return translate('filter-field-pipe.file-size');
      default:
        throw new Error(`Invalid FilterField value: ${value}`);
    }
  }

}
