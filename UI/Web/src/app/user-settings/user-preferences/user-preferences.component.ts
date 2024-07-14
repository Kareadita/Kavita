import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  OnDestroy,
  OnInit
} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {ToastrService} from 'ngx-toastr';
import {take} from 'rxjs/operators';
import {Title} from '@angular/platform-browser';
import {
  bookLayoutModes,
  bookWritingStyles,
  layoutModes,
  pageLayoutModes,
  pageSplitOptions,
  pdfLayoutModes,
  pdfScrollModes,
  pdfSpreadModes,
  pdfThemes,
  Preferences,
  readingDirections,
  readingModes,
  scalingOptions
} from 'src/app/_models/preferences/preferences';
import {User} from 'src/app/_models/user';
import {AccountService} from 'src/app/_services/account.service';
import {ActivatedRoute, Router, RouterLink} from '@angular/router';
import {SettingsService} from 'src/app/admin/settings.service';
import {BookPageLayoutMode} from 'src/app/_models/readers/book-page-layout-mode';
import {forkJoin} from 'rxjs';
import {bookColorThemes} from 'src/app/book-reader/_components/reader-settings/reader-settings.component';
import {BookService} from 'src/app/book-reader/_services/book.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {SentenceCasePipe} from '../../_pipes/sentence-case.pipe';
import {UserHoldsComponent} from '../user-holds/user-holds.component';
import {UserScrobbleHistoryComponent} from '../../_single-module/user-scrobble-history/user-scrobble-history.component';
import {UserStatsComponent} from '../../statistics/_components/user-stats/user-stats.component';
import {ManageDevicesComponent} from '../manage-devices/manage-devices.component';
import {ThemeManagerComponent} from '../theme-manager/theme-manager.component';
import {ApiKeyComponent} from '../api-key/api-key.component';
import {ColorPickerModule} from 'ngx-color-picker';
import {ChangeAgeRestrictionComponent} from '../change-age-restriction/change-age-restriction.component';
import {ChangePasswordComponent} from '../change-password/change-password.component';
import {ChangeEmailComponent} from '../change-email/change-email.component';
import {NgFor, NgIf, NgTemplateOutlet, TitleCasePipe} from '@angular/common';
import {
  NgbAccordionBody,
  NgbAccordionButton,
  NgbAccordionCollapse,
  NgbAccordionDirective,
  NgbAccordionHeader,
  NgbAccordionItem,
  NgbAccordionToggle,
  NgbCollapse,
  NgbNav,
  NgbNavContent,
  NgbNavItem,
  NgbNavItemRole,
  NgbNavLink,
  NgbNavOutlet,
  NgbTooltip
} from '@ng-bootstrap/ng-bootstrap';
import {
  SideNavCompanionBarComponent
} from '../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {LocalizationService} from "../../_services/localization.service";
import {Language} from "../../_models/metadata/language";
import {translate, TranslocoDirective} from "@ngneat/transloco";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {ManageScrobblingProvidersComponent} from "../manage-scrobbling-providers/manage-scrobbling-providers.component";
import {PdfLayoutModePipe} from "../../pdf-reader/_pipe/pdf-layout-mode.pipe";
import {PdfTheme} from "../../_models/preferences/pdf-theme";
import {PdfScrollMode} from "../../_models/preferences/pdf-scroll-mode";
import {PdfLayoutMode} from "../../_models/preferences/pdf-layout-mode";
import {PdfSpreadMode} from "../../_models/preferences/pdf-spread-mode";
import {
  MangaUserPreferencesComponent
} from "../../src/app/user-settings/manga-user-preferences/manga-user-preferences.component";

enum AccordionPanelID {
  ImageReader = 'image-reader',
  BookReader = 'book-reader',
  GlobalSettings = 'global-settings',
  PdfReader = 'pdf-reader'
}

enum FragmentID {
  Account = 'account',
  Preferences = '',
  Clients = 'clients',
  Theme = 'theme',
  Devices = 'devices',
  Stats = 'stats',
  Scrobbling = 'scrobbling'
}

@Component({
  selector: 'app-user-preferences',
  templateUrl: './user-preferences.component.html',
  styleUrls: ['./user-preferences.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [SideNavCompanionBarComponent, NgbNav, NgFor, NgbNavItem, NgbNavItemRole, NgbNavLink, RouterLink, NgbNavContent, NgIf, ChangeEmailComponent,
    ChangePasswordComponent, ChangeAgeRestrictionComponent, ReactiveFormsModule, NgbAccordionDirective, NgbAccordionItem, NgbAccordionHeader,
    NgbAccordionToggle, NgbAccordionButton, NgbCollapse, NgbAccordionCollapse, NgbAccordionBody, NgbTooltip, NgTemplateOutlet, ColorPickerModule, ApiKeyComponent,
    ThemeManagerComponent, ManageDevicesComponent, UserStatsComponent, UserScrobbleHistoryComponent, UserHoldsComponent, NgbNavOutlet, TitleCasePipe, SentenceCasePipe,
    TranslocoDirective, LoadingComponent, ManageScrobblingProvidersComponent, PdfLayoutModePipe, MangaUserPreferencesComponent],
})
export class UserPreferencesComponent implements OnInit, OnDestroy {

  private readonly destroyRef = inject(DestroyRef);
  private readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);
  private readonly bookService = inject(BookService);
  private readonly titleService = inject(Title);
  private readonly route = inject(ActivatedRoute);
  private readonly settingsService = inject(SettingsService);
  private readonly router = inject(Router);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly localizationService = inject(LocalizationService);
  protected readonly AccordionPanelID = AccordionPanelID;
  protected readonly FragmentID = FragmentID;


  user: User | undefined = undefined;

  observableHandles: Array<any> = [];

  tabs: Array<{title: string, fragment: string}> = [
    {title: 'account-tab', fragment: FragmentID.Account},
    {title: 'preferences-tab', fragment: FragmentID.Preferences},
    {title: '3rd-party-clients-tab', fragment: FragmentID.Clients},
    {title: 'theme-tab', fragment: FragmentID.Theme},
    {title: 'devices-tab', fragment: FragmentID.Devices},
    {title: 'stats-tab', fragment: FragmentID.Stats},
  ];
  locales: Array<Language> = [{title: 'English', isoCode: 'en'}];
  active = this.tabs[1];
  opdsEnabled: boolean = false;
  opdsUrl: string = '';
  makeUrl: (val: string) => string = (val: string) => { return this.opdsUrl; };
  hasActiveLicense = false;



  constructor() {


    this.accountService.getOpdsUrl().subscribe(res => {
      this.opdsUrl = res;
      this.cdRef.markForCheck();
    });

    this.settingsService.getOpdsEnabled().subscribe(res => {
      this.opdsEnabled = res;
      this.cdRef.markForCheck();
    });



    this.accountService.hasValidLicense$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(res => {
      if (res) {
        if (this.tabs.filter(t => t.fragment == FragmentID.Scrobbling).length === 0) {
          this.tabs.push({title: 'scrobbling-tab', fragment: FragmentID.Scrobbling});
        }

        this.hasActiveLicense = true;
        this.cdRef.markForCheck();
      }

      this.route.fragment.subscribe(frag => {
        const tab = this.tabs.filter(item => item.fragment === frag);
        if (tab.length > 0) {
          this.active = tab[0];
        } else {
          this.active = this.tabs[1]; // Default to preferences
        }
        this.cdRef.markForCheck();
      });
    });



  }

  ngOnInit(): void {
    this.titleService.setTitle('Kavita - User Preferences');

    forkJoin({
      user: this.accountService.currentUser$.pipe(take(1)),
      pref: this.accountService.getPreferences()
    }).subscribe(results => {
      if (results.user === undefined) {
        this.router.navigateByUrl('/login');
        return;
      }

      this.user = results.user;
      this.user.preferences = results.pref;
      this.cdRef.markForCheck();
    });
  }

  ngOnDestroy() {
    this.observableHandles.forEach(o => o.unsubscribe());
  }
}
