import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
} from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { distinctUntilChanged, take } from 'rxjs';
import { ThemeService } from 'src/app/_services/theme.service';
import { SiteTheme } from 'src/app/_models/preferences/site-theme';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { SiteThemeProviderPipe } from '../../_pipes/site-theme-provider.pipe';
import { SentenceCasePipe } from '../../_pipes/sentence-case.pipe';
import {NgIf, NgFor, AsyncPipe, NgTemplateOutlet} from '@angular/common';
import {translate, TranslocoDirective} from "@ngneat/transloco";
import {shareReplay, tap} from "rxjs/operators";
import {NgbModal} from "@ng-bootstrap/ng-bootstrap";
import {BrowseThemesComponent} from "../browse-themes/browse-themes.component";
import {CarouselReelComponent} from "../../carousel/_components/carousel-reel/carousel-reel.component";
import {SeriesCardComponent} from "../../cards/series-card/series-card.component";
import {ImageComponent} from "../../shared/image/image.component";
import {DownloadableSiteTheme} from "../../_models/theme/downloadable-site-theme";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {SafeUrlPipe} from "../../_pipes/safe-url.pipe";

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
  private readonly modalService = inject(NgbModal);
  protected readonly themeService = inject(ThemeService);
  private readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);
  private readonly cdRef = inject(ChangeDetectorRef);

  currentTheme: SiteTheme | undefined;
  isAdmin: boolean = false;
  user: User | undefined;
  selectedTheme: DownloadableSiteTheme | undefined;
  browseableThemes$ = this.themeService.getDownloadableThemes()
    .pipe(takeUntilDestroyed(this.destroyRef), shareReplay({refCount: true, bufferSize: 1}), tap(_ => console.log('calling browse api')));

  constructor() {

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

  browse() {
    this.modalService.open(BrowseThemesComponent, { scrollable: true, size: 'md', fullscreen: 'md' });
  }

  scan() {
    this.themeService.scan().subscribe(() => {
      this.toastr.info(translate('theme-manager.scan-queued'));
    });
  }

  selectTheme(theme: SiteTheme | DownloadableSiteTheme) {
    if (theme.hasOwnProperty('provider')) {
      // Convert into DownloadableSiteTheme
      this.selectedTheme = {
        isCompatible: true,
        previewUrls: [],
        lastCompatibleVersion: '',
        name: theme.name,
        alreadyDownloaded: true,
        author: 'N/A',
        cssUrl: '',
        description: 'System Provided'
      } as DownloadableSiteTheme;
    } else {
      this.selectedTheme = theme as DownloadableSiteTheme;
    }

    this.cdRef.markForCheck();
  }
}
