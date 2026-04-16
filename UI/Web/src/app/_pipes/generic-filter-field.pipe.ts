import {Pipe, PipeTransform} from '@angular/core';
import {SeriesFilterField} from "../_models/metadata/v2/series-filter-field";
import {translate} from "@jsverse/transloco";
import {ValidFilterEntity} from "../metadata-filter/filter-settings";
import {PersonFilterField} from "../_models/metadata/v2/person-filter-field";
import {AnnotationsFilterField} from "../_models/metadata/v2/annotations-filter";
import {ReadingListFilterField} from "../_models/metadata/v2/reading-list-filter-field";

@Pipe({
  name: 'genericFilterField'
})
export class GenericFilterFieldPipe implements PipeTransform {

  transform<T extends number>(value: T, entityType: ValidFilterEntity): string {

    switch (entityType) {
      case 'annotation':
        return this.annotationsFilterField(value as AnnotationsFilterField);
      case 'series':
        return this.translateFilterField(value as SeriesFilterField);
      case 'person':
        return this.translatePersonFilterField(value as PersonFilterField);
      case 'readinglist':
        return this.translateReadingListFilterField(value as ReadingListFilterField);
    }
  }

  private annotationsFilterField(value: AnnotationsFilterField) {
    switch (value) {
      case AnnotationsFilterField.Likes:
        return translate('generic-filter-field-pipe.annotation-likes')
      case AnnotationsFilterField.LikedBy:
        return translate('generic-filter-field-pipe.annotation-liked-by')
      case AnnotationsFilterField.Selection:
        return translate('generic-filter-field-pipe.annotation-selection')
      case AnnotationsFilterField.Comment:
        return translate('generic-filter-field-pipe.annotation-comment')
      case AnnotationsFilterField.HighlightSlots:
        return translate('generic-filter-field-pipe.annotation-highlights')
      case AnnotationsFilterField.Owner:
        return translate('generic-filter-field-pipe.annotation-owner');
      case AnnotationsFilterField.Library:
        return translate('filter-field-pipe.libraries');
      case AnnotationsFilterField.Spoiler:
        return translate('generic-filter-field-pipe.annotation-spoiler');
      case AnnotationsFilterField.Series:
        return translate('generic-filter-field-pipe.series');
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
      case PersonFilterField.Library:
        return translate('generic-filter-field-pipe.person-library');
    }
  }

  private translateReadingListFilterField(value: ReadingListFilterField) {
    switch (value) {
      case ReadingListFilterField.Title:
        return translate('generic-filter-field-pipe.readinglist-title');
      case ReadingListFilterField.ReleaseYear:
        return translate('generic-filter-field-pipe.readinglist-release-year');
      case ReadingListFilterField.ItemCount:
        return translate('generic-filter-field-pipe.readinglist-item-count');
      case ReadingListFilterField.Tags:
        return translate('generic-filter-field-pipe.readinglist-tags');
      case ReadingListFilterField.Writer:
        return translate('generic-filter-field-pipe.readinglist-writer');
      case ReadingListFilterField.Artist:
        return translate('generic-filter-field-pipe.readinglist-artist');
    }
  }

  private translateFilterField(value: SeriesFilterField) {
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
