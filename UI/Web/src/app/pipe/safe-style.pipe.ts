import { Pipe, PipeTransform } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';

@Pipe({
  name: 'safeStyle'
})
export class SafeStylePipe implements PipeTransform {

  constructor(private sanitizer: DomSanitizer){
  }

  transform(style: string) {
      return this.sanitizer.bypassSecurityTrustStyle(style);
  }

}
