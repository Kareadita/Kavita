import { Pipe, PipeTransform } from '@angular/core';
import { ThemeProvider } from 'src/app/_models/preferences/site-theme';


@Pipe({
    name: 'siteThemeProvider',
    standalone: true
})
export class SiteThemeProviderPipe implements PipeTransform {

  transform(provider: ThemeProvider | undefined | null): string {
    if (provider === null || provider === undefined) return '';
    switch(provider) {
      case ThemeProvider.System:
        return 'System';
      case ThemeProvider.User:
        return 'User';
      default:
        return '';
    }
  }

}
