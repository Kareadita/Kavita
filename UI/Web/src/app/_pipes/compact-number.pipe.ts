import { Pipe, PipeTransform } from '@angular/core';
import {AccountService} from "../_services/account.service";

const specialCases = [4, 7, 10, 13];

@Pipe({
  name: 'compactNumber',
  standalone: true
})
export class CompactNumberPipe implements PipeTransform {

  constructor() {}

  transform(value: number): string {
    // Weblate allows some non-standard languages, like 'zh_Hans', which should be just 'zh'. So we handle that here
    const key = localStorage.getItem(AccountService.localeKey)?.replace('_', '-');
    if (key?.endsWith('Hans')) {
      return this.transformValue(key?.split('-')[0] || 'en', value);
    }
    return this.transformValue(key || 'en', value);
  }

  private transformValue(locale: string, value: number) {
    const formatter = new Intl.NumberFormat(locale, {
      //@ts-ignore
      notation: 'compact', // https://github.com/microsoft/TypeScript/issues/36533
      maximumSignificantDigits: 3
    });

    const formatterForDoublePrecision = new Intl.NumberFormat(locale, {
      //@ts-ignore
      notation: 'compact', // https://github.com/microsoft/TypeScript/issues/36533
      maximumSignificantDigits: 2
    });

    if (value < 1000) return value + '';
    if (specialCases.includes((value + '').length)) { // from 4, every 3 will have a case where we need to override
      return formatterForDoublePrecision.format(value);
    }

    return formatter.format(value);
  }



}
