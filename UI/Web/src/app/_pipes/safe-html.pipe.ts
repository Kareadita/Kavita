import { inject } from '@angular/core';
import { Pipe, PipeTransform, SecurityContext } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';

@Pipe({
  name: 'safeHtml',
  pure: true,
  standalone: true
})
export class SafeHtmlPipe implements PipeTransform {
  private readonly dom: DomSanitizer = inject(DomSanitizer);
  constructor() {}

  transform(value: string): unknown {
    return this.dom.sanitize(SecurityContext.HTML, value);
  }

}
