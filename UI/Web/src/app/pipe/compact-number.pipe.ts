import { Pipe, PipeTransform } from '@angular/core';


const formatter = new Intl.NumberFormat('en-GB', { 
  //@ts-ignore
  notation: 'compact', // https://github.com/microsoft/TypeScript/issues/36533
  maximumSignificantDigits: 3
});

const formatterForDoublePercision = new Intl.NumberFormat('en-GB', { 
  //@ts-ignore
  notation: 'compact', // https://github.com/microsoft/TypeScript/issues/36533
  maximumSignificantDigits: 2
});

const specialCases = [4, 7, 10, 13];

@Pipe({
  name: 'compactNumber'
})
export class CompactNumberPipe implements PipeTransform {

  transform(value: number): string {

    if (value < 1000) return value + '';
    if (specialCases.includes(value + ''.length)) { // from 4, every 3 will have a case where we need to override
      return formatterForDoublePercision.format(value);
    }
    
    return formatter.format(value);
  }

}
