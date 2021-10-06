import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'sentenceCase'
})
export class SentenceCasePipe implements PipeTransform {

  transform(value: string | null): string {
    if (value === null || value === undefined) return '';
    
    return value.charAt(0).toUpperCase() + value.substr(1);
  }

}
