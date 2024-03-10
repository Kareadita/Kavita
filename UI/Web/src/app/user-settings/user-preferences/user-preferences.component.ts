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
import {take, tap} from 'rxjs/operators';
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
import { SentenceCasePipe } from '../../_pipes/sentence-case.pipe';
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
import {LoadingComponent} from "../../shared/loading/loading.component";

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
    TranslocoDirective, LoadingComponent],
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
  hasActiveLicense = false;
  canEdit = true;

  //Initialize form controls
  readingDirectionControl = new FormControl();
  scalingOptionControl = new FormControl();
  pageSplitOptionControl = new FormControl();
  autoCloseMenuControl = new FormControl();
  showScreenHintsControl = new FormControl();
  readerModeControl = new FormControl();
  layoutModeControl = new FormControl();
  emulateBookControl = new FormControl();
  swipeToPaginateControl = new FormControl();

  bookReaderFontFamilyControl = new FormControl();
  bookReaderFontSizeControl = new FormControl();
  bookReaderLineSpacingControl = new FormControl();
  bookReaderMarginControl = new FormControl();
  bookReaderReadingDirectionControl = new FormControl();
  bookReaderWritingStyleControl = new FormControl();
  bookReaderTapToPaginateControl = new FormControl();
  bookReaderSwipeToPaginateControl = new FormControl();
  bookReaderScrollThresholdControl = new FormControl();
  bookReaderDistanceThresholdControl = new FormControl();
  bookReaderSpeedThresholdControl = new FormControl();
  bookReaderLayoutModeControl = new FormControl();
  bookReaderThemeNameControl = new FormControl();
  bookReaderImmersiveModeControl = new FormControl();

  themeControl = new FormControl();
  globalPageLayoutModeControl = new FormControl();
  blurUnreadSummariesControl = new FormControl();
  promptForDownloadSizeControl = new FormControl();
  noTransitionsControl = new FormControl();
  collapseSeriesRelationshipsControl = new FormControl();
  shareReviewsControl = new FormControl()
  localeControl = new FormControl();



  constructor() {
    this.fontFamilies = this.bookService.getFontFamilies().map(f => f.title);
    this.cdRef.markForCheck();

    this.accountService.getOpdsUrl().subscribe(res => {
      this.opdsUrl = res;
      this.cdRef.markForCheck();
    });

    this.settingsService.getOpdsEnabled().subscribe(res => {
      this.opdsEnabled = res;
      this.cdRef.markForCheck();
    });

    this.localizationService.getLocales().subscribe(res => {
      this.locales = res;
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
        console.log('tab: ', tab);
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

      if (this.fontFamilies.indexOf(this.user.preferences.bookReaderFontFamily) < 0) {
        this.user.preferences.bookReaderFontFamily = 'default';
      }

      //Add form controls to form group
      this.settingsForm.addControl('readingDirection', this.readingDirectionControl);
      this.settingsForm.addControl('scalingOption', this.scalingOptionControl);
      this.settingsForm.addControl('pageSplitOption', this.pageSplitOptionControl);
      this.settingsForm.addControl('autoCloseMenu', this.autoCloseMenuControl);
      this.settingsForm.addControl('showScreenHints', this.showScreenHintsControl);
      this.settingsForm.addControl('readerMode', this.readerModeControl);
      this.settingsForm.addControl('layoutMode', this.layoutModeControl);
      this.settingsForm.addControl('emulateBook', this.emulateBookControl);
      this.settingsForm.addControl('swipeToPaginate', this.swipeToPaginateControl);

      this.settingsForm.addControl('bookReaderFontFamily', this.bookReaderFontFamilyControl);
      this.settingsForm.addControl('bookReaderFontSize', this.bookReaderFontSizeControl);
      this.settingsForm.addControl('bookReaderLineSpacing', this.bookReaderLineSpacingControl);
      this.settingsForm.addControl('bookReaderMargin', this.bookReaderMarginControl);
      this.settingsForm.addControl('bookReaderReadingDirection', this.bookReaderReadingDirectionControl);
      this.settingsForm.addControl('bookReaderWritingStyle', this.bookReaderWritingStyleControl);
      this.settingsForm.addControl('bookReaderTapToPaginate', this.bookReaderTapToPaginateControl);
      this.settingsForm.addControl('bookReaderSwipeToPaginate', this.bookReaderSwipeToPaginateControl);
      this.settingsForm.addControl('bookReaderScrollThreshold', this.bookReaderScrollThresholdControl);
      this.settingsForm.addControl('bookReaderDistanceThreshold', this.bookReaderDistanceThresholdControl);
      this.settingsForm.addControl('bookReaderSpeedThreshold', this.bookReaderSpeedThresholdControl);
      this.settingsForm.addControl('bookReaderLayoutMode', this.bookReaderLayoutModeControl);
      this.settingsForm.addControl('bookReaderThemeName', this.bookReaderThemeNameControl);
      this.settingsForm.addControl('bookReaderImmersiveMode', this.bookReaderImmersiveModeControl);

      this.settingsForm.addControl('theme', this.themeControl);
      this.settingsForm.addControl('globalPageLayoutMode', this.globalPageLayoutModeControl);
      this.settingsForm.addControl('blurUnreadSummaries', this.blurUnreadSummariesControl);
      this.settingsForm.addControl('promptForDownloadSize', this.promptForDownloadSizeControl);
      this.settingsForm.addControl('noTransitions', this.noTransitionsControl);
      this.settingsForm.addControl('collapseSeriesRelationships', this.collapseSeriesRelationshipsControl);
      this.settingsForm.addControl('shareReviews', this.shareReviewsControl);
      this.settingsForm.addControl('locale', this.localeControl);

      //Set initial values here because they're retrieved from the account service
      this.readingDirectionControl.setValue(this.user?.preferences.readingDirection);
      this.scalingOptionControl.setValue(this.user?.preferences.scalingOption);
      this.pageSplitOptionControl.setValue(this.user?.preferences.pageSplitOption);
      this.autoCloseMenuControl.setValue(this.user?.preferences.autoCloseMenu);
      this.showScreenHintsControl.setValue(this.user?.preferences.showScreenHints);
      this.readerModeControl.setValue(this.user?.preferences.readerMode);
      this.layoutModeControl.setValue(this.user?.preferences.layoutMode);
      this.emulateBookControl.setValue(this.user?.preferences.emulateBook);
      this.swipeToPaginateControl.setValue(this.user?.preferences.swipeToPaginate);

      this.bookReaderFontFamilyControl.setValue(this.user?.preferences.bookReaderFontFamily);
      this.bookReaderFontSizeControl.setValue(this.user?.preferences.bookReaderFontSize);
      this.bookReaderLineSpacingControl.setValue(this.user?.preferences.bookReaderLineSpacing);
      this.bookReaderMarginControl.setValue(this.user?.preferences.bookReaderMargin);
      this.bookReaderReadingDirectionControl.setValue(this.user?.preferences.bookReaderReadingDirection);
      this.bookReaderWritingStyleControl.setValue(this.user?.preferences.bookReaderWritingStyle);
      this.bookReaderTapToPaginateControl.setValue(!!this.user?.preferences.bookReaderTapToPaginate);
      this.bookReaderSwipeToPaginateControl.setValue(!!this.user?.preferences.bookReaderSwipeToPaginate);
      this.bookReaderScrollThresholdControl.setValue(this.user?.preferences.bookReaderScrollThreshold);
      this.bookReaderDistanceThresholdControl.setValue(this.user?.preferences.bookReaderDistanceThreshold);
      this.bookReaderSpeedThresholdControl.setValue(this.user?.preferences.bookReaderSpeedThreshold);
      this.bookReaderLayoutModeControl.setValue(this.user?.preferences.bookReaderLayoutMode || BookPageLayoutMode.Default);
      this.bookReaderThemeNameControl.setValue(this.user?.preferences.bookReaderThemeName || bookColorThemes[0].name);
      this.bookReaderImmersiveModeControl.setValue(this.user?.preferences.bookReaderImmersiveMode);

      this.themeControl.setValue(this.user?.preferences.theme);
      this.globalPageLayoutModeControl.setValue(this.user?.preferences.globalPageLayoutMode);
      this.blurUnreadSummariesControl.setValue(this.user?.preferences.blurUnreadSummaries);
      this.promptForDownloadSizeControl.setValue(this.user?.preferences.promptForDownloadSize);
      this.noTransitionsControl.setValue(this.user?.preferences.noTransitions);
      this.collapseSeriesRelationshipsControl.setValue(this.user?.preferences.collapseSeriesRelationships);
      this.shareReviewsControl.setValue(this.user?.preferences.shareReviews);
      this.localeControl.setValue(this.user?.preferences.locale);

      if (this.locales.length === 1) {
        this.localeControl.disable();
      }

      //When Tap to Paginate is enabled, force disable swipe to paginate
      this.bookReaderTapToPaginateControl.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((tapToPaginate: any) => {
        if (tapToPaginate) {
          this.bookReaderSwipeToPaginateControl.setValue(false);
        }
      });

      //When Swipe to Paginate is enabled, force disable tap to paginate
      this.bookReaderSwipeToPaginateControl.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((swipeToPaginate: any) => {
        if (swipeToPaginate) {
          this.bookReaderTapToPaginateControl.setValue(false);
        }
      });

      this.cdRef.markForCheck();
    });

    this.cdRef.markForCheck();
  }

  ngOnDestroy() {
    this.observableHandles.forEach(o => o.unsubscribe());
  }


  resetForm() {
    if (this.user === undefined) { return; }
    this.readingDirectionControl.setValue(this.user.preferences.readingDirection);
    this.scalingOptionControl.setValue(this.user.preferences.scalingOption);
    this.autoCloseMenuControl.setValue(this.user.preferences.autoCloseMenu);
    this.showScreenHintsControl.setValue(this.user.preferences.showScreenHints);
    this.readerModeControl.setValue(this.user.preferences.readerMode);
    this.layoutModeControl.setValue(this.user.preferences.layoutMode);
    this.pageSplitOptionControl.setValue(this.user.preferences.pageSplitOption);
    this.bookReaderFontFamilyControl.setValue(this.user.preferences.bookReaderFontFamily);
    this.bookReaderFontSizeControl.setValue(this.user.preferences.bookReaderFontSize);
    this.bookReaderLineSpacingControl.setValue(this.user.preferences.bookReaderLineSpacing);
    this.bookReaderMarginControl.setValue(this.user.preferences.bookReaderMargin);
    this.bookReaderTapToPaginateControl.setValue(this.user.preferences.bookReaderTapToPaginate);
    this.bookReaderSwipeToPaginateControl.setValue(this.user.preferences.bookReaderSwipeToPaginate);
    this.bookReaderScrollThresholdControl.setValue(this.user?.preferences.bookReaderScrollThreshold);
    this.bookReaderDistanceThresholdControl.setValue(this.user?.preferences.bookReaderDistanceThreshold);
    this.bookReaderSpeedThresholdControl.setValue(this.user?.preferences.bookReaderSpeedThreshold);
    this.bookReaderReadingDirectionControl.setValue(this.user.preferences.bookReaderReadingDirection);
    this.bookReaderWritingStyleControl.setValue(this.user.preferences.bookReaderWritingStyle);
    this.bookReaderLayoutModeControl.setValue(this.user.preferences.bookReaderLayoutMode);
    this.bookReaderThemeNameControl.setValue(this.user.preferences.bookReaderThemeName);
    this.themeControl.setValue(this.user.preferences.theme);
    this.bookReaderImmersiveModeControl.setValue(this.user.preferences.bookReaderImmersiveMode);
    this.globalPageLayoutModeControl.setValue(this.user.preferences.globalPageLayoutMode);
    this.blurUnreadSummariesControl.setValue(this.user.preferences.blurUnreadSummaries);
    this.promptForDownloadSizeControl.setValue(this.user.preferences.promptForDownloadSize);
    this.noTransitionsControl.setValue(this.user.preferences.noTransitions);
    this.emulateBookControl.setValue(this.user.preferences.emulateBook);
    this.swipeToPaginateControl.setValue(this.user.preferences.swipeToPaginate);
    this.collapseSeriesRelationshipsControl.setValue(this.user.preferences.collapseSeriesRelationships);
    this.shareReviewsControl.setValue(this.user.preferences.shareReviews);
    this.localeControl.setValue(this.user.preferences.locale);
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
      bookReaderSwipeToPaginate: modelSettings.bookReaderSwipeToPaginate,
      bookReaderScrollThreshold: parseFloat(modelSettings.bookReaderScrollThreshold),
      bookReaderDistanceThreshold: parseFloat(modelSettings.bookReaderDistanceThreshold),
      bookReaderSpeedThreshold: parseFloat(modelSettings.bookReaderSpeedThreshold),
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

    this.observableHandles.push(this.accountService.updatePreferences(data).subscribe((updatedPrefs: any) => {
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

  protected readonly undefined = undefined;
}
