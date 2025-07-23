import {Pipe, PipeTransform} from '@angular/core';
import {MetadataFieldType} from "../admin/_models/metadata-settings";
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'metadataFieldType'
})
export class MetadataFieldTypePipe implements PipeTransform {

  transform(value: MetadataFieldType | null | string): string {
    if (typeof value === 'string') {
      value = parseInt(value, 10);
    }

    switch (value) {
      case MetadataFieldType.Genre:
        return translate('manage-metadata-settings.genre');
      case MetadataFieldType.Tag:
        return translate('manage-metadata-settings.tag');
      default:
        return translate('common.unknown');
    }
  }

}
