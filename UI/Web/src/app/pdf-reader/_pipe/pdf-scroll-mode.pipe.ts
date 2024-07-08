import {inject, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from "@ngneat/transloco";
import {ScrollModeType} from "ngx-extended-pdf-viewer";

@Pipe({
  name: 'pdfScrollMode',
  standalone: true
})
export class PdfScrollModePipe implements PipeTransform {
  translocoService = inject(TranslocoService);
  transform(value: ScrollModeType): string {
    switch (value) {
      case ScrollModeType.vertical:
        return this.translocoService.translate('pdf-scroll-mode-pipe.vertical');
      case ScrollModeType.horizontal:
        return this.translocoService.translate('pdf-scroll-mode-pipe.horizontal');
      case ScrollModeType.wrapped:
        return this.translocoService.translate('pdf-scroll-mode-pipe.wrapped');
      case ScrollModeType.page:
        return this.translocoService.translate('pdf-scroll-mode-pipe.page');
    }
  }

}
