import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
} from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import {distinctUntilChanged, map, take} from 'rxjs';
import { ThemeService } from 'src/app/_services/theme.service';
import {SiteTheme, ThemeProvider} from 'src/app/_models/preferences/site-theme';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { SiteThemeProviderPipe } from '../../_pipes/site-theme-provider.pipe';
import { SentenceCasePipe } from '../../_pipes/sentence-case.pipe';
import {NgIf, NgFor, AsyncPipe, NgTemplateOutlet} from '@angular/common';
import {translate, TranslocoDirective} from "@ngneat/transloco";
import {filter, shareReplay} from "rxjs/operators";
import {CarouselReelComponent} from "../../carousel/_components/carousel-reel/carousel-reel.component";
import {SeriesCardComponent} from "../../cards/series-card/series-card.component";
import {ImageComponent} from "../../shared/image/image.component";
import {DownloadableSiteTheme} from "../../_models/theme/downloadable-site-theme";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {SafeUrlPipe} from "../../_pipes/safe-url.pipe";
import {ScrobbleProvider} from "../../_services/scrobbling.service";
import {ConfirmService} from "../../shared/confirm.service";

interface ThemeContainer {
  downloadable?: DownloadableSiteTheme;
  site?: SiteTheme;
  isSiteTheme: boolean;
  name: string;
}

@Component({
    selector: 'app-theme-manager',
    templateUrl: './theme-manager.component.html',
    styleUrls: ['./theme-manager.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [NgIf, NgFor, AsyncPipe, SentenceCasePipe, SiteThemeProviderPipe, TranslocoDirective, CarouselReelComponent, SeriesCardComponent, ImageComponent, DefaultValuePipe, NgTemplateOutlet, SafeUrlPipe]
})
export class ThemeManagerComponent {
  private readonly destroyRef = inject(DestroyRef);
  protected readonly themeService = inject(ThemeService);
  private readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly confirmService = inject(ConfirmService);

  protected readonly ThemeProvider = ThemeProvider;
  protected readonly ScrobbleProvider = ScrobbleProvider;

  currentTheme: SiteTheme | undefined;
  isAdmin: boolean = false;
  user: User | undefined;
  selectedTheme: ThemeContainer | undefined;
  downloadableThemes: Array<DownloadableSiteTheme> = [];
  downloadableThemes$ = this.themeService.getDownloadableThemes()
    .pipe(takeUntilDestroyed(this.destroyRef), shareReplay({refCount: true, bufferSize: 1}));
  hasAdmin$ = this.accountService.currentUser$.pipe(
    takeUntilDestroyed(this.destroyRef), shareReplay({refCount: true, bufferSize: 1}),
    map(c => c && this.accountService.hasAdminRole(c))
  );

  constructor() {

    this.downloadableThemes$.subscribe(d => {
      this.downloadableThemes = d;
      this.cdRef.markForCheck();
    });

    this.themeService.currentTheme$.pipe(takeUntilDestroyed(this.destroyRef), distinctUntilChanged()).subscribe(theme => {
      this.currentTheme = theme;
      this.cdRef.markForCheck();
    });

    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.user = user;
        this.isAdmin = this.accountService.hasAdminRole(user);
        this.cdRef.markForCheck();
      }
    });
  }

  async deleteTheme(theme: SiteTheme) {
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-theme'))) {
      return;
    }

    this.themeService.deleteTheme(theme.id).subscribe(_ => {
      this.removeDownloadedTheme(theme);
    });
  }

  removeDownloadedTheme(theme: SiteTheme) {
    this.selectedTheme = undefined;
    this.downloadableThemes = this.downloadableThemes.filter(d => d.name !== theme.name);
    this.cdRef.markForCheck();
  }

  applyTheme(theme: SiteTheme) {
    if (!this.user) return;

    const pref = Object.assign({}, this.user.preferences);
    pref.theme = theme;
    this.accountService.updatePreferences(pref).subscribe();
  }

  updateDefault(theme: SiteTheme) {
    this.themeService.setDefault(theme.id).subscribe(() => {
      this.toastr.success(translate('theme-manager.updated-toastr', {name: theme.name}));
    });
  }

  selectTheme(theme: SiteTheme | DownloadableSiteTheme) {
    if (theme.hasOwnProperty('provider')) {
      this.selectedTheme = {
        isSiteTheme: true,
        site: theme as SiteTheme,
        name: theme.name
      };
    } else {
      this.selectedTheme = {
        isSiteTheme: false,
        downloadable: theme as DownloadableSiteTheme,
        name: theme.name
      };
    }

    this.cdRef.markForCheck();
  }

  downloadTheme(theme: DownloadableSiteTheme) {
    this.themeService.downloadTheme(theme).subscribe(theme => {
      this.removeDownloadedTheme(theme);
    });
  }
}
