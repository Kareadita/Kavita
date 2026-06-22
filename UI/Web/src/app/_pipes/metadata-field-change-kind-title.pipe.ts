import {inject, Pipe, PipeTransform} from '@angular/core';
import {MetadataFieldChangeKind} from "../_models/kavitaplus/metadata-field-change-kind.enum";
import {TranslocoService} from "@jsverse/transloco";

@Pipe({
  name: 'metadataFieldChangeKindTitle',
})
export class MetadataFieldChangeKindTitlePipe implements PipeTransform {

  private readonly translocoService = inject(TranslocoService);

  transform(value: MetadataFieldChangeKind): string {
    const key = this.getKey(value);
    return this.translocoService.translate(`metadata-field-change-kind-title-pipe.${key}`);
  }

  private getKey(value: MetadataFieldChangeKind): string {
    switch (value) {
      case MetadataFieldChangeKind.Relationships:
        return 'relationships';
      case MetadataFieldChangeKind.Characters:
        return 'characters';
      case MetadataFieldChangeKind.Artists:
        return 'artists';
      case MetadataFieldChangeKind.Writers:
        return 'writers';
      case MetadataFieldChangeKind.Tags:
        return 'tags';
      case MetadataFieldChangeKind.Genres:
        return 'genres';
      case MetadataFieldChangeKind.PublicationStatus:
        return 'publication-status';
      case MetadataFieldChangeKind.AgeRating:
        return 'age-rating';
      case MetadataFieldChangeKind.ExternalIds:
        return 'external-ids';
      case MetadataFieldChangeKind.Summary:
        return 'summary';
      case MetadataFieldChangeKind.Title:
        return 'title';
      case MetadataFieldChangeKind.ReleaseDate:
        return 'release-date';
      case MetadataFieldChangeKind.ReleaseYear:
        return 'release-year';
      case MetadataFieldChangeKind.LocalizedName:
        return 'localized-name';
    }
  }

}
