import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {translate, TranslocoDirective} from "@ngneat/transloco";
import {
  bookLayoutModes,
  bookWritingStyles,
  layoutModes,
  pageLayoutModes,
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
import {ToastrService} from "ngx-toastr";
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
import {NgForOf, NgIf, NgTemplateOutlet, TitleCasePipe} from "@angular/common";
import {ColorPickerModule} from "ngx-color-picker";
import {SettingTitleComponent} from "../../settings/_components/setting-title/setting-title.component";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {PageLayoutModePipe} from "../../_pipes/page-layout-mode.pipe";
import {SettingSwitchComponent} from "../../settings/_components/setting-switch/setting-switch.component";

@Component({
  selector: 'app-manga-user-preferences',
  standalone: true,
  imports: [
    TranslocoDirective,
    NgbAccordionDirective,
    ReactiveFormsModule,
    NgbAccordionItem,
    NgbAccordionCollapse,
    NgbAccordionBody,
    NgbAccordionHeader,
    NgbAccordionButton,
    NgIf,
    NgbTooltip,
    NgTemplateOutlet,
    TitleCasePipe,
    ColorPickerModule,
    NgForOf,
    SettingTitleComponent,
    SettingItemComponent,
    PageLayoutModePipe,
    SettingSwitchComponent
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
  // pdfLayoutModesTranslated = pdfLayoutModes.map(this.translatePrefOptions);
  pdfScrollModesTranslated = pdfScrollModes.map(this.translatePrefOptions);
  pdfSpreadModesTranslated = pdfSpreadModes.map(this.translatePrefOptions);
  pdfThemesTranslated = pdfThemes.map(this.translatePrefOptions);

  fontFamilies: Array<string> = [];
  locales: Array<Language> = [{title: 'English', isoCode: 'en'}];

  settingsForm: FormGroup = new FormGroup({});
  user: User | undefined = undefined;



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

      if (this.locales.length === 1) {
        this.settingsForm.get('locale')?.disable();
      }

      this.settingsForm.valueChanges.pipe(
        distinctUntilChanged(),
        debounceTime(100),
        filter(_ => this.settingsForm.valid),
        takeUntilDestroyed(this.destroyRef),
        switchMap(_ => {
          const data = this.packSettings();
          return this.accountService.updatePreferences(data);
        }),
        tap(updatedPrefs => {
          if (this.user) {
            this.user.preferences = updatedPrefs;
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
      backgroundColor: this.user?.preferences.backgroundColor || '#000',
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
      locale: modelSettings.locale,
      pdfTheme: parseInt(modelSettings.pdfTheme, 10),
      pdfScrollMode: parseInt(modelSettings.pdfScrollMode, 10),
      pdfSpreadMode: parseInt(modelSettings.pdfSpreadMode, 10),
    };
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
