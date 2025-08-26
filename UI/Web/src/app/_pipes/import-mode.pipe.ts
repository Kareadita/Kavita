import { Pipe, PipeTransform } from '@angular/core';
import {translate} from "@jsverse/transloco";
import {ImportMode} from "../_models/import-field-mappings";

@Pipe({
  name: 'importMode'
})
export class ImportModePipe implements PipeTransform {

  transform(value: ImportMode | null | string): string {
    if (typeof value === 'string') {
      value = parseInt(value, 10);
    }

    switch (value) {
      case ImportMode.Replace:
        return translate('import-mappings.replace');
      case ImportMode.Merge:
        return translate('import-mappings.merge');
    }

    return translate('common.unknown');
  }

}
