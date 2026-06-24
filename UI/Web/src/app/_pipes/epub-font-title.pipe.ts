import { Pipe, PipeTransform } from '@angular/core';
import {EpubFont, FontProvider} from "../_models/preferences/epub-font";
import {translate} from "@jsverse/transloco";
import {TitleCasePipe} from "@angular/common";

@Pipe({
  name: 'epubFontTitle',
})
export class EpubFontTitlePipe implements PipeTransform {

  private readonly titleCasePipe = new TitleCasePipe();
  transform(value: EpubFont, includeSuffix: boolean = true): string {
    const family = this.titleCasePipe.transform(value.family);
    if (includeSuffix && value.provider === FontProvider.User) {
      return family + " " + translate('epub-font-title-pipe.user-font-suffix');
    }

    return family;
  }

}
