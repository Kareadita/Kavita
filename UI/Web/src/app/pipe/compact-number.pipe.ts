import { Pipe, PipeTransform } from '@angular/core';


const formatter = new Intl.NumberFormat('en-GB', { 
  //@ts-ignore
  notation: 'compact', // https://github.com/microsoft/TypeScript/issues/36533
  maximumSignificantDigits: 3
});

@Pipe({
  name: 'compactNumber'
})
export class CompactNumberPipe implements PipeTransform {

  transform(value: number): string {
    return formatter.format(value);
  }

}
