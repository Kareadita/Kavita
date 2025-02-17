import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {translate, TranslocoDirective, TranslocoService} from "@jsverse/transloco";
import {
  bookLayoutModes,
  bookWritingStyles,
  layoutModes,
  pageSplitOptions,
  pdfScrollModes,
  pdfSpreadModes,
  pdfThemes,
  Preferences,
  readingDirections,
  readingModes,
  scalingOptions
} from "../../_models/preferences/preferences";
import {AccountService} from "../../_services/account.service";
import {BookService} from "../../book-reader/_services/book.service";
import {Title} from "@angular/platform-browser";
import {Router} from "@angular/router";
import {LocalizationService} from "../../_services/localization.service";
import {bookColorThemes} from "../../book-reader/_components/reader-settings/reader-settings.component";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {User} from "../../_models/user";
import {Language} from "../../_models/metadata/language";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {debounceTime, distinctUntilChanged, filter, forkJoin, switchMap, tap} from "rxjs";
import {take} from "rxjs/operators";
import {BookPageLayoutMode} from "../../_models/readers/book-page-layout-mode";
import {PdfTheme} from "../../_models/preferences/pdf-theme";
import {PdfScrollMode} from "../../_models/preferences/pdf-scroll-mode";
import {PdfSpreadMode} from "../../_models/preferences/pdf-spread-mode";
import {
  NgbAccordionBody, NgbAccordionButton,
  NgbAccordionCollapse,
  NgbAccordionDirective, NgbAccordionHeader,
  NgbAccordionItem, NgbTooltip
} from "@ng-bootstrap/ng-bootstrap";
import {AsyncPipe, NgStyle, NgTemplateOutlet, TitleCasePipe} from "@angular/common";
import {ColorPickerModule} from "ngx-color-picker";
import {SettingTitleComponent} from "../../settings/_components/setting-title/setting-title.component";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {PageLayoutModePipe} from "../../_pipes/page-layout-mode.pipe";
import {SettingSwitchComponent} from "../../settings/_components/setting-switch/setting-switch.component";
import {ReadingDirectionPipe} from "../../_pipes/reading-direction.pipe";
import {ScalingOptionPipe} from "../../_pipes/scaling-option.pipe";
import {PageSplitOptionPipe} from "../../_pipes/page-split-option.pipe";
import {ReaderModePipe} from "../../_pipes/reading-mode.pipe";
import {LayoutModePipe} from "../../_pipes/layout-mode.pipe";
import {WritingStylePipe} from "../../_pipes/writing-style.pipe";
import {BookPageLayoutModePipe} from "../../_pipes/book-page-layout-mode.pipe";
import {PdfSpreadTypePipe} from "../../pdf-reader/_pipe/pdf-spread-mode.pipe";
import {PdfSpreadModePipe} from "../../_pipes/pdf-spread-mode.pipe";
import {PdfThemePipe} from "../../_pipes/pdf-theme.pipe";
import {PdfScrollModeTypePipe} from "../../pdf-reader/_pipe/pdf-scroll-mode.pipe";
import {PdfScrollModePipe} from "../../_pipes/pdf-scroll-mode.pipe";
import {LicenseService} from "../../_services/license.service";

@Component({
  selector: 'app-manga-user-preferences',
  standalone: true,
  imports: [
    TranslocoDirective,
    ReactiveFormsModule,
    TitleCasePipe,
    ColorPickerModule,
    SettingItemComponent,
    SettingSwitchComponent,
    ReadingDirectionPipe,
    ScalingOptionPipe,
    PageSplitOptionPipe,
    ReaderModePipe,
    LayoutModePipe,
    NgStyle,
    WritingStylePipe,
    BookPageLayoutModePipe,
    PdfSpreadModePipe,
    PdfThemePipe,
    PdfScrollModePipe,
    AsyncPipe
  ],
  templateUrl: './manage-user-preferences.component.html',
  styleUrl: './manage-user-preferences.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageUserPreferencesComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);
  private readonly accountService = inject(AccountService);
  private readonly bookService = inject(BookService);
  private readonly titleService = inject(Title);
  private readonly router = inject(Router);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly localizationService = inject(LocalizationService);
  protected readonly licenseService = inject(LicenseService);

  protected readonly readingDirections = readingDirections;
  protected readonly scalingOptions = scalingOptions;
  protected readonly pageSplitOptions = pageSplitOptions;
  protected readonly readerModes = readingModes;
  protected readonly layoutModes = layoutModes;
  protected readonly bookWritingStyles = bookWritingStyles;
  protected readonly bookLayoutModes = bookLayoutModes;
  protected readonly pdfSpreadModes = pdfSpreadModes;
  protected readonly pdfThemes = pdfThemes;
  protected readonly pdfScrollModes = pdfScrollModes;

  bookColorThemesTranslated = bookColorThemes.map(o => {
    const d = {...o};
    d.name = translate('theme.' + d.translationKey);
    return d;
  });


  fontFamilies: Array<string> = [];
  locales: Array<Language> = [{title: 'English', isoCode: 'en'}];

  settingsForm: FormGroup = new FormGroup({});
  user: User | undefined = undefined;

  get Locale() {
    if (!this.settingsForm.get('locale')) return 'English';

    return this.locales.filter(l => l.isoCode === this.settingsForm.get('locale')!.value)[0].title;
  }


  constructor() {
    this.fontFamilies = this.bookService.getFontFamilies().map(f => f.title);
    this.cdRef.markForCheck();

    this.localizationService.getLocales().subscribe(res => {
      this.locales = res;

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
      this.settingsForm.addControl('backgroundColor', new FormControl(this.user.preferences.backgroundColor, []));

      this.settingsForm.addControl('bookReaderFontFamily', new FormControl(this.user.preferences.bookReaderFontFamily, []));
      this.settingsForm.addControl('bookReaderFontSize', new FormControl(this.user.preferences.bookReaderFontSize, []));
      this.settingsForm.addControl('bookReaderLineSpacing', new FormControl(this.user.preferences.bookReaderLineSpacing, []));
      this.settingsForm.addControl('bookReaderMargin', new FormControl(this.user.preferences.bookReaderMargin, []));
      this.settingsForm.addControl('bookReaderReadingDirection', new FormControl(this.user.preferences.bookReaderReadingDirection, []));
      this.settingsForm.addControl('bookReaderWritingStyle', new FormControl(this.user.preferences.bookReaderWritingStyle, []))
      this.settingsForm.addControl('bookReaderTapToPaginate', new FormControl(this.user.preferences.bookReaderTapToPaginate, []));
      this.settingsForm.addControl('bookReaderLayoutMode', new FormControl(this.user.preferences.bookReaderLayoutMode || BookPageLayoutMode.Default, []));
      this.settingsForm.addControl('bookReaderThemeName', new FormControl(this.user?.preferences.bookReaderThemeName || bookColorThemes[0].name, []));
      this.settingsForm.addControl('bookReaderImmersiveMode', new FormControl(this.user?.preferences.bookReaderImmersiveMode, []));

      this.settingsForm.addControl('pdfTheme', new FormControl(this.user?.preferences.pdfTheme || PdfTheme.Dark, []));
      this.settingsForm.addControl('pdfScrollMode', new FormControl(this.user?.preferences.pdfScrollMode || PdfScrollMode.Vertical, []));
      this.settingsForm.addControl('pdfSpreadMode', new FormControl(this.user?.preferences.pdfSpreadMode || PdfSpreadMode.None, []));

      this.settingsForm.addControl('theme', new FormControl(this.user.preferences.theme, []));
      this.settingsForm.addControl('globalPageLayoutMode', new FormControl(this.user.preferences.globalPageLayoutMode, []));
      this.settingsForm.addControl('blurUnreadSummaries', new FormControl(this.user.preferences.blurUnreadSummaries, []));
      this.settingsForm.addControl('promptForDownloadSize', new FormControl(this.user.preferences.promptForDownloadSize, []));
      this.settingsForm.addControl('noTransitions', new FormControl(this.user.preferences.noTransitions, []));
      this.settingsForm.addControl('collapseSeriesRelationships', new FormControl(this.user.preferences.collapseSeriesRelationships, []));
      this.settingsForm.addControl('shareReviews', new FormControl(this.user.preferences.shareReviews, []));
      this.settingsForm.addControl('locale', new FormControl(this.user.preferences.locale || 'en', []));

      this.settingsForm.addControl('aniListScrobblingEnabled', new FormControl(this.user.preferences.aniListScrobblingEnabled || false, []));
      this.settingsForm.addControl('wantToReadSync', new FormControl(this.user.preferences.wantToReadSync || false, []));


      // Automatically save settings as we edit them
      this.settingsForm.valueChanges.pipe(
        distinctUntilChanged(),
        debounceTime(100),
        filter(_ => this.settingsForm.valid),
        takeUntilDestroyed(this.destroyRef),
        switchMap(_ => {
          const data = this.packSettings();
          return this.accountService.updatePreferences(data);
        }),
        tap(prefs => {
          if (this.user) {
            this.user.preferences = {...prefs};
            this.cdRef.markForCheck();
          }
        })
      ).subscribe();

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

  reset() {
    if (!this.user) return;

    this.settingsForm.get('readingDirection')?.setValue(this.user.preferences.readingDirection, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('scalingOption')?.setValue(this.user.preferences.scalingOption, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('pageSplitOption')?.setValue(this.user.preferences.pageSplitOption, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('autoCloseMenu')?.setValue(this.user.preferences.autoCloseMenu, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('showScreenHints')?.setValue(this.user.preferences.showScreenHints, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('readerMode')?.setValue(this.user.preferences.readerMode, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('layoutMode')?.setValue(this.user.preferences.layoutMode, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('emulateBook')?.setValue(this.user.preferences.emulateBook, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('swipeToPaginate')?.setValue(this.user.preferences.swipeToPaginate, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('backgroundColor')?.setValue(this.user.preferences.backgroundColor, {onlySelf: true, emitEvent: false});

    this.settingsForm.get('bookReaderFontFamily')?.setValue(this.user.preferences.bookReaderFontFamily, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('bookReaderFontSize')?.setValue(this.user.preferences.bookReaderFontSize, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('bookReaderLineSpacing')?.setValue(this.user.preferences.bookReaderLineSpacing, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('bookReaderMargin')?.setValue(this.user.preferences.bookReaderMargin, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('bookReaderReadingDirection')?.setValue(this.user.preferences.bookReaderReadingDirection, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('bookReaderWritingStyle')?.setValue(this.user.preferences.bookReaderWritingStyle, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('bookReaderTapToPaginate')?.setValue(this.user.preferences.bookReaderTapToPaginate, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('bookReaderLayoutMode')?.setValue(this.user.preferences.bookReaderLayoutMode || BookPageLayoutMode.Default, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('bookReaderThemeName')?.setValue(this.user?.preferences.bookReaderThemeName || bookColorThemes[0].name, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('bookReaderImmersiveMode')?.setValue(this.user?.preferences.bookReaderImmersiveMode, {onlySelf: true, emitEvent: false});

    this.settingsForm.get('pdfTheme')?.setValue(this.user?.preferences.pdfTheme || PdfTheme.Dark, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('pdfScrollMode')?.setValue(this.user?.preferences.pdfScrollMode || PdfScrollMode.Vertical, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('pdfSpreadMode')?.setValue(this.user?.preferences.pdfSpreadMode || PdfSpreadMode.None, {onlySelf: true, emitEvent: false});

    this.settingsForm.get('theme')?.setValue(this.user.preferences.theme, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('globalPageLayoutMode')?.setValue(this.user.preferences.globalPageLayoutMode, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('blurUnreadSummaries')?.setValue(this.user.preferences.blurUnreadSummaries, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('promptForDownloadSize')?.setValue(this.user.preferences.promptForDownloadSize, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('noTransitions')?.setValue(this.user.preferences.noTransitions, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('collapseSeriesRelationships')?.setValue(this.user.preferences.collapseSeriesRelationships, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('shareReviews')?.setValue(this.user.preferences.shareReviews, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('locale')?.setValue(this.user.preferences.locale || 'en', {onlySelf: true, emitEvent: false});

    this.settingsForm.get('aniListScrobblingEnabled')?.setValue(this.user.preferences.aniListScrobblingEnabled || false, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('wantToReadSync')?.setValue(this.user.preferences.wantToReadSync || false, {onlySelf: true, emitEvent: false});
  }

  packSettings(): Preferences {
    const modelSettings = this.settingsForm.value;
    return  {
      readingDirection: parseInt(modelSettings.readingDirection, 10),
      scalingOption: parseInt(modelSettings.scalingOption, 10),
      pageSplitOption: parseInt(modelSettings.pageSplitOption, 10),
      autoCloseMenu: modelSettings.autoCloseMenu,
      readerMode: parseInt(modelSettings.readerMode, 10),
      layoutMode: parseInt(modelSettings.layoutMode, 10),
      showScreenHints: modelSettings.showScreenHints,
      backgroundColor: modelSettings.backgroundColor || '#000',
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
      locale: modelSettings.locale || 'en',
      pdfTheme: parseInt(modelSettings.pdfTheme, 10),
      pdfScrollMode: parseInt(modelSettings.pdfScrollMode, 10),
      pdfSpreadMode: parseInt(modelSettings.pdfSpreadMode, 10),
      aniListScrobblingEnabled: modelSettings.aniListScrobblingEnabled,
      wantToReadSync: modelSettings.wantToReadSync
    };
  }

  handleBackgroundColorChange(color: string) {
    this.settingsForm.markAsDirty();
    this.settingsForm.markAsTouched();
    if (this.user?.preferences) {
      this.user.preferences.backgroundColor = color;
    }

    this.settingsForm.get('backgroundColor')?.setValue(color);
    this.cdRef.markForCheck();
  }
}
