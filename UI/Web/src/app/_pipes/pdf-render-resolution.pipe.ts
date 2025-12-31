import {Pipe, PipeTransform} from '@angular/core';
import {PdfRenderResolution} from "../admin/_models/pdf-render-resolution";
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'pdfRenderResolution',
  standalone: true
})
export class PdfRenderResolutionPipe implements PipeTransform {
  transform(value: PdfRenderResolution): string {
    switch (value) {
      case PdfRenderResolution.Default:
        return translate('pdf-render-resolution.default');
      case PdfRenderResolution.High:
        return translate('pdf-render-resolution.high');
      case PdfRenderResolution.Ultra:
        return translate('pdf-render-resolution.ultra');
    }
  }
}
