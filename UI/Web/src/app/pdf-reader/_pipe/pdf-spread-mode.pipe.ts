import {inject, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from "@ngneat/transloco";
import {SpreadType} from "ngx-extended-pdf-viewer/lib/options/spread-type";

@Pipe({
  name: 'pdfSpreadMode',
  standalone: true
})
export class PdfSpreadModePipe implements PipeTransform {
  translocoService = inject(TranslocoService);

  transform(value: SpreadType): string {
    switch (value) {
      case 'off' as SpreadType:
        return this.translocoService.translate('pdf-spread-mode-pipe.off');
      case "even":
        return this.translocoService.translate('pdf-spread-mode-pipe.even');
      case "odd":
        return this.translocoService.translate('pdf-spread-mode-pipe.odd');
    }
    return this.translocoService.translate('pdf-spread-mode-pipe.off');
  }

}
