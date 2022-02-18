import { ThemeProvider } from '../../_models/preferences/site-theme';
import { SiteThemeProviderPipe } from './site-theme-provider.pipe';

describe('SiteThemeProviderPipe', () => {
  let siteThemeProviderPipe: SiteThemeProviderPipe;

  beforeEach(() => {
    siteThemeProviderPipe = new SiteThemeProviderPipe();
  })

  it('translates system to System', () => {
    expect(siteThemeProviderPipe.transform(ThemeProvider.System)).toBe('System');
  });

  it('translates user to User', () => {
    expect(siteThemeProviderPipe.transform(ThemeProvider.User)).toBe('User');
  });

  it('translates null to empty string', () => {
    expect(siteThemeProviderPipe.transform(null)).toBe('');
  });

  it('translates undefined to empty string', () => {
    expect(siteThemeProviderPipe.transform(undefined)).toBe('');
  });
});
