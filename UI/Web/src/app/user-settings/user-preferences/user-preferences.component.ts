import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  OnDestroy,
  OnInit
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { Title } from '@angular/platform-browser';
import {
  readingDirections,
  scalingOptions,
  pageSplitOptions,
  readingModes,
  Preferences,
  bookLayoutModes,
  layoutModes,
  pageLayoutModes,
  bookWritingStyles
} from 'src/app/_models/preferences/preferences';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { SettingsService } from 'src/app/admin/settings.service';
import { BookPageLayoutMode } from 'src/app/_models/readers/book-page-layout-mode';
import {forkJoin} from 'rxjs';
import { bookColorThemes } from 'src/app/book-reader/_components/reader-settings/reader-settings.component';
import { BookService } from 'src/app/book-reader/_services/book.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { SentenceCasePipe } from '../../pipe/sentence-case.pipe';
import { UserHoldsComponent } from '../user-holds/user-holds.component';
import { UserScrobbleHistoryComponent } from '../../_single-module/user-scrobble-history/user-scrobble-history.component';
import { UserStatsComponent } from '../../statistics/_components/user-stats/user-stats.component';
import { ManageDevicesComponent } from '../manage-devices/manage-devices.component';
import { ThemeManagerComponent } from '../theme-manager/theme-manager.component';
import { ApiKeyComponent } from '../api-key/api-key.component';
import { ColorPickerModule } from 'ngx-color-picker';
import { AnilistKeyComponent } from '../anilist-key/anilist-key.component';
import { ChangeAgeRestrictionComponent } from '../change-age-restriction/change-age-restriction.component';
import { ChangePasswordComponent } from '../change-password/change-password.component';
import { ChangeEmailComponent } from '../change-email/change-email.component';
import { NgFor, NgIf, NgTemplateOutlet, TitleCasePipe } from '@angular/common';
import { NgbNav, NgbNavItem, NgbNavItemRole, NgbNavLink, NgbNavContent, NgbAccordionDirective, NgbAccordionItem, NgbAccordionHeader, NgbAccordionToggle, NgbAccordionButton, NgbCollapse, NgbAccordionCollapse, NgbAccordionBody, NgbTooltip, NgbNavOutlet } from '@ng-bootstrap/ng-bootstrap';
import { SideNavCompanionBarComponent } from '../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {LocalizationService} from "../../_services/localization.service";
import {Language} from "../../_models/metadata/language";
import {translate, TranslocoDirective} from "@ngneat/transloco";

enum AccordionPanelID {
  ImageReader = 'image-reader',
  BookReader = 'book-reader',
  GlobalSettings = 'global-settings'
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
    ChangePasswordComponent, ChangeAgeRestrictionComponent, AnilistKeyComponent, ReactiveFormsModule, NgbAccordionDirective, NgbAccordionItem, NgbAccordionHeader,
    NgbAccordionToggle, NgbAccordionButton, NgbCollapse, NgbAccordionCollapse, NgbAccordionBody, NgbTooltip, NgTemplateOutlet, ColorPickerModule, ApiKeyComponent,
    ThemeManagerComponent, ManageDevicesComponent, UserStatsComponent, UserScrobbleHistoryComponent, UserHoldsComponent, NgbNavOutlet, TitleCasePipe, SentenceCasePipe,
    TranslocoDirective]
})
export class UserPreferencesComponent implements OnInit, OnDestroy {

  readingDirectionsTranslated = readingDirections.map(this.translatePrefOptions);
  scalingOptionsTranslated = scalingOptions.map(this.translatePrefOptions);
  pageSplitOptionsTranslated = pageSplitOptions.map(this.translatePrefOptions);
  readingModesTranslated = readingModes.map(this.translatePrefOptions);
  layoutModesTranslated = layoutModes.map(this.translatePrefOptions);
  bookLayoutModesTranslated = bookLayoutModes.map(this.translatePrefOptions);
  bookColorThemesTranslated = bookColorThemes.map(o => {
    const d = {...o};
    d.name = translate('theme.' + d.translationKey);
    return d;
  });

  pageLayoutModesTranslated = pageLayoutModes.map(this.translatePrefOptions);
  bookWritingStylesTranslated = bookWritingStyles.map(this.translatePrefOptions);

  settingsForm: FormGroup = new FormGroup({});
  user: User | undefined = undefined;

  observableHandles: Array<any> = [];
  fontFamilies: Array<string> = [];

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
  private readonly destroyRef = inject(DestroyRef);

  get AccordionPanelID() {
    return AccordionPanelID;
  }

  get FragmentID() {
    return FragmentID;
  }


  constructor(private accountService: AccountService, private toastr: ToastrService, private bookService: BookService,
    private titleService: Title, private route: ActivatedRoute, private settingsService: SettingsService,
    private router: Router, private readonly cdRef: ChangeDetectorRef, public localizationService: LocalizationService) {
    this.fontFamilies = this.bookService.getFontFamilies().map(f => f.title);
    this.cdRef.markForCheck();

    this.accountService.getOpdsUrl().subscribe(res => {
      this.opdsUrl = res;
      this.cdRef.markForCheck();
    });

    this.localizationService.getLocales().subscribe(res => {
      this.locales = res;
      this.cdRef.markForCheck();
    });

    this.accountService.hasValidLicense().subscribe(res => {
      if (res) {
        this.tabs.push({title: 'scrobbling-tab', fragment: FragmentID.Scrobbling});
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
    })


    this.settingsService.getOpdsEnabled().subscribe(res => {
      this.opdsEnabled = res;
      this.cdRef.markForCheck();
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

      if (this.fontFamilies.indexOf(this.user.preferences.bookReaderFontFamily) < 0) {
        this.user.preferences.bookReaderFontFamily = 'default';
      }

      this.settingsForm.addControl('readingDirection', new FormControl(this.user.preferences.readingDirection, []));
      this.settingsForm.addControl('scalingOption', new FormControl(this.user.preferences.scalingOption, []));
      this.settingsForm.addControl('pageSplitOption', new FormControl(this.user.preferences.pageSplitOption, []));
      this.settingsForm.addControl('autoCloseMenu', new FormControl(this.user.preferences.autoCloseMenu, []));
      this.settingsForm.addControl('showScreenHints', new FormControl(this.user.preferences.showScreenHints, []));
      this.settingsForm.addControl('readerMode', new FormControl(this.user.preferences.readerMode, []));
      this.settingsForm.addControl('layoutMode', new FormControl(this.user.preferences.layoutMode, []));
      this.settingsForm.addControl('emulateBook', new FormControl(this.user.preferences.emulateBook, []));
      this.settingsForm.addControl('swipeToPaginate', new FormControl(this.user.preferences.swipeToPaginate, []));

      this.settingsForm.addControl('bookReaderFontFamily', new FormControl(this.user.preferences.bookReaderFontFamily, []));
      this.settingsForm.addControl('bookReaderFontSize', new FormControl(this.user.preferences.bookReaderFontSize, []));
      this.settingsForm.addControl('bookReaderLineSpacing', new FormControl(this.user.preferences.bookReaderLineSpacing, []));
      this.settingsForm.addControl('bookReaderMargin', new FormControl(this.user.preferences.bookReaderMargin, []));
      this.settingsForm.addControl('bookReaderReadingDirection', new FormControl(this.user.preferences.bookReaderReadingDirection, []));
      this.settingsForm.addControl('bookReaderWritingStyle', new FormControl(this.user.preferences.bookReaderWritingStyle, []))
      this.settingsForm.addControl('bookReaderTapToPaginate', new FormControl(!!this.user.preferences.bookReaderTapToPaginate, []));
      this.settingsForm.addControl('bookReaderLayoutMode', new FormControl(this.user.preferences.bookReaderLayoutMode || BookPageLayoutMode.Default, []));
      this.settingsForm.addControl('bookReaderThemeName', new FormControl(this.user?.preferences.bookReaderThemeName || bookColorThemes[0].name, []));
      this.settingsForm.addControl('bookReaderImmersiveMode', new FormControl(this.user?.preferences.bookReaderImmersiveMode, []));

      this.settingsForm.addControl('theme', new FormControl(this.user.preferences.theme, []));
      this.settingsForm.addControl('globalPageLayoutMode', new FormControl(this.user.preferences.globalPageLayoutMode, []));
      this.settingsForm.addControl('blurUnreadSummaries', new FormControl(this.user.preferences.blurUnreadSummaries, []));
      this.settingsForm.addControl('promptForDownloadSize', new FormControl(this.user.preferences.promptForDownloadSize, []));
      this.settingsForm.addControl('noTransitions', new FormControl(this.user.preferences.noTransitions, []));
      this.settingsForm.addControl('collapseSeriesRelationships', new FormControl(this.user.preferences.collapseSeriesRelationships, []));
      this.settingsForm.addControl('shareReviews', new FormControl(this.user.preferences.shareReviews, []));
      this.settingsForm.addControl('locale', new FormControl(this.user.preferences.locale, []));

      if (this.locales.length === 1) {
        this.settingsForm.get('locale')?.disable();
      }

      this.cdRef.markForCheck();
    });

    this.settingsForm.get('bookReaderImmersiveMode')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(mode => {
      if (mode) {
        this.settingsForm.get('bookReaderTapToPaginate')?.setValue(true);
        this.cdRef.markForCheck();
      }
    });
    this.cdRef.markForCheck();
  }

  ngOnDestroy() {
    this.observableHandles.forEach(o => o.unsubscribe());
  }


  resetForm() {
    if (this.user === undefined) { return; }
    this.settingsForm.get('readingDirection')?.setValue(this.user.preferences.readingDirection);
    this.settingsForm.get('scalingOption')?.setValue(this.user.preferences.scalingOption);
    this.settingsForm.get('autoCloseMenu')?.setValue(this.user.preferences.autoCloseMenu);
    this.settingsForm.get('showScreenHints')?.setValue(this.user.preferences.showScreenHints);
    this.settingsForm.get('readerMode')?.setValue(this.user.preferences.readerMode);
    this.settingsForm.get('layoutMode')?.setValue(this.user.preferences.layoutMode);
    this.settingsForm.get('pageSplitOption')?.setValue(this.user.preferences.pageSplitOption);
    this.settingsForm.get('bookReaderFontFamily')?.setValue(this.user.preferences.bookReaderFontFamily);
    this.settingsForm.get('bookReaderFontSize')?.setValue(this.user.preferences.bookReaderFontSize);
    this.settingsForm.get('bookReaderLineSpacing')?.setValue(this.user.preferences.bookReaderLineSpacing);
    this.settingsForm.get('bookReaderMargin')?.setValue(this.user.preferences.bookReaderMargin);
    this.settingsForm.get('bookReaderTapToPaginate')?.setValue(this.user.preferences.bookReaderTapToPaginate);
    this.settingsForm.get('bookReaderReadingDirection')?.setValue(this.user.preferences.bookReaderReadingDirection);
    this.settingsForm.get('bookReaderWritingStyle')?.setValue(this.user.preferences.bookReaderWritingStyle);
    this.settingsForm.get('bookReaderLayoutMode')?.setValue(this.user.preferences.bookReaderLayoutMode);
    this.settingsForm.get('bookReaderThemeName')?.setValue(this.user.preferences.bookReaderThemeName);
    this.settingsForm.get('theme')?.setValue(this.user.preferences.theme);
    this.settingsForm.get('bookReaderImmersiveMode')?.setValue(this.user.preferences.bookReaderImmersiveMode);
    this.settingsForm.get('globalPageLayoutMode')?.setValue(this.user.preferences.globalPageLayoutMode);
    this.settingsForm.get('blurUnreadSummaries')?.setValue(this.user.preferences.blurUnreadSummaries);
    this.settingsForm.get('promptForDownloadSize')?.setValue(this.user.preferences.promptForDownloadSize);
    this.settingsForm.get('noTransitions')?.setValue(this.user.preferences.noTransitions);
    this.settingsForm.get('emulateBook')?.setValue(this.user.preferences.emulateBook);
    this.settingsForm.get('swipeToPaginate')?.setValue(this.user.preferences.swipeToPaginate);
    this.settingsForm.get('collapseSeriesRelationships')?.setValue(this.user.preferences.collapseSeriesRelationships);
    this.settingsForm.get('shareReviews')?.setValue(this.user.preferences.shareReviews);
    this.settingsForm.get('locale')?.setValue(this.user.preferences.locale);
    this.cdRef.markForCheck();
    this.settingsForm.markAsPristine();
  }

  save() {
    if (this.user === undefined) return;
    const modelSettings = this.settingsForm.value;
    const data: Preferences = {
      readingDirection: parseInt(modelSettings.readingDirection, 10),
      scalingOption: parseInt(modelSettings.scalingOption, 10),
      pageSplitOption: parseInt(modelSettings.pageSplitOption, 10),
      autoCloseMenu: modelSettings.autoCloseMenu,
      readerMode: parseInt(modelSettings.readerMode, 10),
      layoutMode: parseInt(modelSettings.layoutMode, 10),
      showScreenHints: modelSettings.showScreenHints,
      backgroundColor: this.user.preferences.backgroundColor,
      bookReaderFontFamily: modelSettings.bookReaderFontFamily,
      bookReaderLineSpacing: modelSettings.bookReaderLineSpacing,
      bookReaderFontSize: modelSettings.bookReaderFontSize,
      bookReaderMargin: modelSettings.bookReaderMargin,
      bookReaderTapToPaginate: modelSettings.bookReaderTapToPaginate,
      bookReaderReadingDirection: parseInt(modelSettings.bookReaderReadingDirection, 10),
      bookReaderWritingStyle: parseInt(modelSettings.bookReaderWritingStyle, 10),
      bookReaderLayoutMode: parseInt(modelSettings.bookReaderLayoutMode, 10),
      bookReaderThemeName: modelSettings.bookReaderThemeName,
      theme: modelSettings.theme,
      bookReaderImmersiveMode: modelSettings.bookReaderImmersiveMode,
      globalPageLayoutMode: parseInt(modelSettings.globalPageLayoutMode, 10),
      blurUnreadSummaries: modelSettings.blurUnreadSummaries,
      promptForDownloadSize: modelSettings.promptForDownloadSize,
      noTransitions: modelSettings.noTransitions,
      emulateBook: modelSettings.emulateBook,
      swipeToPaginate: modelSettings.swipeToPaginate,
      collapseSeriesRelationships: modelSettings.collapseSeriesRelationships,
      shareReviews: modelSettings.shareReviews,
      locale: modelSettings.locale
    };

    this.observableHandles.push(this.accountService.updatePreferences(data).subscribe((updatedPrefs) => {
      this.toastr.success(translate('user-preferences.success-toast'));
      if (this.user) {
        this.user.preferences = updatedPrefs;
        this.cdRef.markForCheck();
      }
      this.resetForm();
    }));
  }


  handleBackgroundColorChange() {
    this.settingsForm.markAsDirty();
    this.settingsForm.markAsTouched();
    this.cdRef.markForCheck();
  }

  translatePrefOptions(o: {text: string, value: any}) {
    const d = {...o};
    d.text = translate('preferences.' + o.text);
    return d;
  }
}
