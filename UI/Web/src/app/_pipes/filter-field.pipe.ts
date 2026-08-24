import {inject, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from "@jsverse/transloco";
import {SeriesFilterField} from "../_models/metadata/v2/series-filter-field";

@Pipe({
  name: 'filterField',
  standalone: true
})
export class FilterFieldPipe implements PipeTransform {
  readonly translocoService = inject(TranslocoService);

  transform(value: SeriesFilterField): string {
    switch (value) {
      case SeriesFilterField.AgeRating:
        return this.translocoService.translate('filter-field-pipe.age-rating');
      case SeriesFilterField.Characters:
        return this.translocoService.translate('filter-field-pipe.characters');
      case SeriesFilterField.CollectionTags:
        return this.translocoService.translate('filter-field-pipe.collection-tags');
      case SeriesFilterField.Colorist:
        return this.translocoService.translate('filter-field-pipe.colorist');
      case SeriesFilterField.CoverArtist:
        return this.translocoService.translate('filter-field-pipe.cover-artist');
      case SeriesFilterField.Editor:
        return this.translocoService.translate('filter-field-pipe.editor');
      case SeriesFilterField.Formats:
        return this.translocoService.translate('filter-field-pipe.formats');
      case SeriesFilterField.Genres:
        return this.translocoService.translate('filter-field-pipe.genres');
      case SeriesFilterField.Inker:
        return this.translocoService.translate('filter-field-pipe.inker');
      case SeriesFilterField.Imprint:
        return this.translocoService.translate('filter-field-pipe.imprint');
      case SeriesFilterField.Team:
        return this.translocoService.translate('filter-field-pipe.team');
      case SeriesFilterField.Location:
        return this.translocoService.translate('filter-field-pipe.location');
      case SeriesFilterField.Languages:
        return this.translocoService.translate('filter-field-pipe.languages');
      case SeriesFilterField.Libraries:
        return this.translocoService.translate('filter-field-pipe.libraries');
      case SeriesFilterField.Letterer:
        return this.translocoService.translate('filter-field-pipe.letterer');
      case SeriesFilterField.PublicationStatus:
        return this.translocoService.translate('filter-field-pipe.publication-status');
      case SeriesFilterField.Penciller:
        return this.translocoService.translate('filter-field-pipe.penciller');
      case SeriesFilterField.Publisher:
        return this.translocoService.translate('filter-field-pipe.publisher');
      case SeriesFilterField.ReadProgress:
        return this.translocoService.translate('filter-field-pipe.read-progress');
      case SeriesFilterField.ReadTime:
        return this.translocoService.translate('filter-field-pipe.read-time');
      case SeriesFilterField.ReleaseYear:
        return this.translocoService.translate('filter-field-pipe.release-year');
      case SeriesFilterField.SeriesName:
        return this.translocoService.translate('filter-field-pipe.series-name');
      case SeriesFilterField.Summary:
        return this.translocoService.translate('filter-field-pipe.summary');
      case SeriesFilterField.Tags:
        return this.translocoService.translate('filter-field-pipe.tags');
      case SeriesFilterField.Translators:
        return this.translocoService.translate('filter-field-pipe.translators');
      case SeriesFilterField.UserRating:
        return this.translocoService.translate('filter-field-pipe.user-rating');
      case SeriesFilterField.Writers:
        return this.translocoService.translate('filter-field-pipe.writers');
      case SeriesFilterField.Path:
        return this.translocoService.translate('filter-field-pipe.path');
      case SeriesFilterField.FilePath:
        return this.translocoService.translate('filter-field-pipe.file-path');
      case SeriesFilterField.WantToRead:
        return this.translocoService.translate('filter-field-pipe.want-to-read');
      case SeriesFilterField.ReadingDate:
        return this.translocoService.translate('filter-field-pipe.read-date');
        case SeriesFilterField.ReadLast:
        return this.translocoService.translate('filter-field-pipe.read-last');
      case SeriesFilterField.AverageRating:
        return this.translocoService.translate('filter-field-pipe.average-rating');
      case SeriesFilterField.FileSize:
        return this.translocoService.translate('filter-field-pipe.file-size');
      case SeriesFilterField.CollapseSeriesRelationships:
        return this.translocoService.translate('filter-field-pipe.collapse-series-relationships');
      default:
        throw new Error(`Invalid FilterField value: ${value}`);
    }
  }

}
