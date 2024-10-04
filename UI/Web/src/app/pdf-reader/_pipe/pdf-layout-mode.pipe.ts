import {inject, Pipe, PipeTransform} from '@angular/core';
import {PageViewModeType} from "ngx-extended-pdf-viewer";
import {TranslocoService} from "@jsverse/transloco";

@Pipe({
  name: 'pdfLayoutMode',
  standalone: true
})
export class PdfLayoutModePipe implements PipeTransform {

  translocoService = inject(TranslocoService);
  transform(value: PageViewModeType): string {
    switch (value) {
      case "single":
        return this.translocoService.translate('pdf-layout-mode-pipe.single');
      case "book":
        return this.translocoService.translate('pdf-layout-mode-pipe.book');
      case "multiple":
        return this.translocoService.translate('pdf-layout-mode-pipe.multiple');
      case "infinite-scroll":
        return this.translocoService.translate('pdf-layout-mode-pipe.infinite-scroll');

    }
  }

}
