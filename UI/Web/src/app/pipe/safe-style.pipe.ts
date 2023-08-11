import { inject } from '@angular/core';
import { Pipe, PipeTransform } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';

@Pipe({
  name: 'safeStyle',
  standalone: true
})
export class SafeStylePipe implements PipeTransform {
  private readonly sanitizer: DomSanitizer = inject(DomSanitizer);
  constructor(){}

  transform(style: string) {
      return this.sanitizer.bypassSecurityTrustStyle(style);
  }

}
