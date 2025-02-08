import { Pipe, PipeTransform } from '@angular/core';
import {MetadataSettingField} from "../admin/_models/metadata-setting-field";
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'metadataSettingFiled',
  standalone: true
})
export class MetadataSettingFiledPipe implements PipeTransform {

  transform(value: MetadataSettingField): string {
    switch (value) {
      case MetadataSettingField.AgeRating:
        return translate('metadata-setting-field-pipe.age-rating');
        case MetadataSettingField.People:
        return translate('metadata-setting-field-pipe.people');
      case MetadataSettingField.Covers:
        return translate('metadata-setting-field-pipe.covers');
      case MetadataSettingField.Summary:
        return translate('metadata-setting-field-pipe.summary');
      case MetadataSettingField.PublicationStatus:
        return translate('metadata-setting-field-pipe.publication-status');
      case MetadataSettingField.StartDate:
        return translate('metadata-setting-field-pipe.start-date');
      case MetadataSettingField.Genres:
        return translate('metadata-setting-field-pipe.genres');
      case MetadataSettingField.Tags:
        return translate('metadata-setting-field-pipe.tags');
      case MetadataSettingField.LocalizedName:
        return translate('metadata-setting-field-pipe.localized-name');

    }
  }

}
