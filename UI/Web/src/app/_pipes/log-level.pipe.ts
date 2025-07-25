import {Pipe, PipeTransform} from '@angular/core';
import {translate} from "@jsverse/transloco";

/**
 * Transforms the log level string into a localized string
 */
@Pipe({
  name: 'logLevel',
  standalone: true,
  pure: true
})
export class LogLevelPipe implements PipeTransform {
  transform(value: string): string {
    return translate('log-level-pipe.' + value.toLowerCase());
  }

}
