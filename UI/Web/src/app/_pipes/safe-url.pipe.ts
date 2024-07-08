import { inject } from '@angular/core';
import { Pipe, PipeTransform, SecurityContext } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';

@Pipe({
  name: 'safeUrl',
  pure: true,
  standalone: true
})
export class SafeUrlPipe implements PipeTransform {
  private readonly dom: DomSanitizer = inject(DomSanitizer);
  constructor() {}

  transform(value: string | null | undefined): string | null {
    if (value === null || value === undefined) return null;
    return this.dom.sanitize(SecurityContext.URL, value);
  }

}
