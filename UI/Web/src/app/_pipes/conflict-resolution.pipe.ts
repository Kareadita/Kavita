import {Pipe, PipeTransform} from '@angular/core';
import {translate} from "@jsverse/transloco";
import {ConflictResolution} from "../_models/import-field-mappings";

@Pipe({
  name: 'conflictResolution'
})
export class ConflictResolutionPipe implements PipeTransform {

  transform(value: ConflictResolution | null | string): string {
    if (typeof value === 'string') {
      value = parseInt(value, 10);
    }
    switch (value) {
      case ConflictResolution.Manual:
        return translate('import-mappings.manual');
      case ConflictResolution.Keep:
        return translate('import-mappings.keep');
      case ConflictResolution.Replace:
        return translate('import-mappings.replace');
    }

    return translate('common.unknown');
  }

}
