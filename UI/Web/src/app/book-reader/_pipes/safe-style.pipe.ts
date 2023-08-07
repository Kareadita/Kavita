import { Pipe, PipeTransform } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';

@Pipe({
    name: 'safeStyle',
    standalone: true
})
export class SafeStylePipe implements PipeTransform {

  constructor(private sanitizer: DomSanitizer) {}

  transform(value: string): unknown {
    return this.sanitizer.bypassSecurityTrustStyle(value);
  }

}
