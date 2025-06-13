import {Pipe, PipeTransform} from '@angular/core';
import {FilterField} from "../_models/metadata/v2/filter-field";
import {translate} from "@jsverse/transloco";
import {ValidFilterEntity} from "../metadata-filter/filter-settings";
import {PersonFilterField} from "../_models/metadata/v2/person-filter-field";

@Pipe({
  name: 'genericFilterField'
})
export class GenericFilterFieldPipe implements PipeTransform {

  transform<T extends number>(value: T, entityType: ValidFilterEntity): string {

    switch (entityType) {
      case "series":
        return this.translateFilterField(value as FilterField);
      case "person":
        return this.translatePersonFilterField(value as PersonFilterField);
    }
  }

  private translatePersonFilterField(value: PersonFilterField) {
    switch (value) {
      case PersonFilterField.Role:
        return translate('generic-filter-field-pipe.person-role');
      case PersonFilterField.Name:
        return translate('generic-filter-field-pipe.person-name');
      case PersonFilterField.SeriesCount:
        return translate('generic-filter-field-pipe.person-series-count');
      case PersonFilterField.ChapterCount:
        return translate('generic-filter-field-pipe.person-chapter-count');
    }
  }

  private translateFilterField(value: FilterField) {
    switch (value) {
      case FilterField.AgeRating:
        return translate('filter-field-pipe.age-rating');
      case FilterField.Characters:
        return translate('filter-field-pipe.characters');
      case FilterField.CollectionTags:
        return translate('filter-field-pipe.collection-tags');
      case FilterField.Colorist:
        return translate('filter-field-pipe.colorist');
      case FilterField.CoverArtist:
        return translate('filter-field-pipe.cover-artist');
      case FilterField.Editor:
        return translate('filter-field-pipe.editor');
      case FilterField.Formats:
        return translate('filter-field-pipe.formats');
      case FilterField.Genres:
        return translate('filter-field-pipe.genres');
      case FilterField.Inker:
        return translate('filter-field-pipe.inker');
      case FilterField.Imprint:
        return translate('filter-field-pipe.imprint');
      case FilterField.Team:
        return translate('filter-field-pipe.team');
      case FilterField.Location:
        return translate('filter-field-pipe.location');
      case FilterField.Languages:
        return translate('filter-field-pipe.languages');
      case FilterField.Libraries:
        return translate('filter-field-pipe.libraries');
      case FilterField.Letterer:
        return translate('filter-field-pipe.letterer');
      case FilterField.PublicationStatus:
        return translate('filter-field-pipe.publication-status');
      case FilterField.Penciller:
        return translate('filter-field-pipe.penciller');
      case FilterField.Publisher:
        return translate('filter-field-pipe.publisher');
      case FilterField.ReadProgress:
        return translate('filter-field-pipe.read-progress');
      case FilterField.ReadTime:
        return translate('filter-field-pipe.read-time');
      case FilterField.ReleaseYear:
        return translate('filter-field-pipe.release-year');
      case FilterField.SeriesName:
        return translate('filter-field-pipe.series-name');
      case FilterField.Summary:
        return translate('filter-field-pipe.summary');
      case FilterField.Tags:
        return translate('filter-field-pipe.tags');
      case FilterField.Translators:
        return translate('filter-field-pipe.translators');
      case FilterField.UserRating:
        return translate('filter-field-pipe.user-rating');
      case FilterField.Writers:
        return translate('filter-field-pipe.writers');
      case FilterField.Path:
        return translate('filter-field-pipe.path');
      case FilterField.FilePath:
        return translate('filter-field-pipe.file-path');
      case FilterField.WantToRead:
        return translate('filter-field-pipe.want-to-read');
      case FilterField.ReadingDate:
        return translate('filter-field-pipe.read-date');
      case FilterField.ReadLast:
        return translate('filter-field-pipe.read-last');
      case FilterField.AverageRating:
        return translate('filter-field-pipe.average-rating');
      default:
        throw new Error(`Invalid FilterField value: ${value}`);
    }
  }

}
