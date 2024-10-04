import {inject, Pipe, PipeTransform} from '@angular/core';
import { ThemeProvider } from 'src/app/_models/preferences/site-theme';
import {TranslocoService} from "@jsverse/transloco";


@Pipe({
    name: 'siteThemeProvider',
    standalone: true
})
export class SiteThemeProviderPipe implements PipeTransform {

  translocoService = inject(TranslocoService);

  transform(provider: ThemeProvider | undefined | null): string {
    if (provider === null || provider === undefined) return '';
    switch(provider) {
      case ThemeProvider.System:
        return this.translocoService.translate('site-theme-provider-pipe.system');
      case ThemeProvider.Custom:
        return this.translocoService.translate('site-theme-provider-pipe.custom');
      default:
        return '';
    }
  }

}
