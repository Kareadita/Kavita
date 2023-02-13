import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Inject, Input, OnDestroy, OnInit, Output } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { Subject, take, takeUntil } from 'rxjs';
import { BookPageLayoutMode } from 'src/app/_models/readers/book-page-layout-mode';
import { BookTheme } from 'src/app/_models/preferences/book-theme';
import { ReadingDirection } from 'src/app/_models/preferences/reading-direction';
import { ReadingMode } from 'src/app/_models/preferences/reading-mode';
import { ThemeProvider } from 'src/app/_models/preferences/site-theme';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';
import { ThemeService } from 'src/app/_services/theme.service';
import { FontFamily, BookService } from '../../_services/book.service';
import { BookBlackTheme } from '../../_models/book-black-theme';
import { BookDarkTheme } from '../../_models/book-dark-theme';
import { BookWhiteTheme } from '../../_models/book-white-theme';

/**
 * Used for book reader. Do not use for other components
 */
export interface PageStyle {
  'font-family': string;
  'font-size': string;
  'line-height': string;
  'margin-left': string;
  'margin-right': string;
}

export const bookColorThemes = [
  {
    name: 'Dark',
    colorHash: '#292929',
    isDarkTheme: true,
    isDefault: true,
    provider: ThemeProvider.System,
    selector: 'brtheme-dark',
    content: BookDarkTheme
  },
  {
    name: 'Black',
    colorHash: '#000000',
    isDarkTheme: true,
    isDefault: false,
    provider: ThemeProvider.System,
    selector: 'brtheme-black',
    content: BookBlackTheme
  },
  {
    name: 'White',
    colorHash: '#FFFFFF',
    isDarkTheme: false,
    isDefault: false,
    provider: ThemeProvider.System,
    selector: 'brtheme-white',
    content: BookWhiteTheme
  },
];

const mobileBreakpointMarginOverride = 700;

@Component({
  selector: 'app-reader-settings',
  templateUrl: './reader-settings.component.html',
  styleUrls: ['./reader-settings.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReaderSettingsComponent implements OnInit, OnDestroy {
  /**
   * Outputs when clickToPaginate is changed
   */
  @Output() clickToPaginateChanged: EventEmitter<boolean> = new EventEmitter();
  /**
   * Outputs when a style is updated and the reader needs to render it
   */
  @Output() styleUpdate: EventEmitter<PageStyle> = new EventEmitter();
  /**
   * Outputs when a theme/dark mode is updated
   */
  @Output() colorThemeUpdate: EventEmitter<BookTheme> = new EventEmitter();
  /**
   * Outputs when a layout mode is updated
   */
  @Output() layoutModeUpdate: EventEmitter<BookPageLayoutMode> = new EventEmitter();
  /**
   * Outputs when fullscreen is toggled
   */
  @Output() fullscreen: EventEmitter<void> = new EventEmitter();
  /**
   * Outputs when reading direction is changed
   */
  @Output() readingDirection: EventEmitter<ReadingDirection> = new EventEmitter();
  /**
   * Outputs when reading mode is changed
   */
  @Output() bookReaderReadingMode: EventEmitter<ReadingMode> = new EventEmitter();
  /**
   * Outputs when immersive mode is changed
   */
  @Output() immersiveMode: EventEmitter<boolean> = new EventEmitter();

  user!: User;
  /**
   * List of all font families user can select from
   */
  fontOptions: Array<string> = [];
  fontFamilies: Array<FontFamily> = [];
  /**
   * Internal property used to capture all the different css properties to render on all elements
   */
  pageStyles!: PageStyle;

  readingDirectionModel: ReadingDirection = ReadingDirection.LeftToRight;

  readingModeModel: ReadingMode = ReadingMode.Horizontally;


  activeTheme: BookTheme | undefined;

  isFullscreen: boolean = false;

  settingsForm: FormGroup = new FormGroup({});

  /**
   * System provided themes
   */
  themes: Array<BookTheme> = bookColorThemes;


  private onDestroy: Subject<void> = new Subject();


  get BookPageLayoutMode(): typeof BookPageLayoutMode  {
    return BookPageLayoutMode;
  }

  get ReadingDirection() {
    return ReadingDirection;
  }

  get ReadingMode() {
    return ReadingMode;
  }



  constructor(private bookService: BookService, private accountService: AccountService,
    @Inject(DOCUMENT) private document: Document, private themeService: ThemeService,
    private readonly cdRef: ChangeDetectorRef) {}

  ngOnInit(): void {

    this.fontFamilies = this.bookService.getFontFamilies();
    this.fontOptions = this.fontFamilies.map(f => f.title);
    this.cdRef.markForCheck();

    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.user = user;

        if (this.user.preferences.bookReaderFontFamily === undefined) {
          this.user.preferences.bookReaderFontFamily = 'default';
        }
        if (this.user.preferences.bookReaderFontSize === undefined || this.user.preferences.bookReaderFontSize < 50) {
          this.user.preferences.bookReaderFontSize = 100;
        }
        if (this.user.preferences.bookReaderLineSpacing === undefined || this.user.preferences.bookReaderLineSpacing < 100) {
          this.user.preferences.bookReaderLineSpacing = 100;
        }
        if (this.user.preferences.bookReaderMargin === undefined) {
          this.user.preferences.bookReaderMargin = 0;
        }
        if (this.user.preferences.bookReaderReadingDirection === undefined) {
          this.user.preferences.bookReaderReadingDirection = ReadingDirection.LeftToRight;
        }
        if (this.user.preferences.bookReaderReadingMode === undefined) {
          console.log('Reading mode is undefined  - setting to horizontal');
          this.user.preferences.bookReaderReadingMode = ReadingMode.Horizontally;
        }
        this.readingDirectionModel = this.user.preferences.bookReaderReadingDirection;
        this.readingModeModel = this.user.preferences.bookReaderReadingMode;



        this.settingsForm.addControl('bookReaderFontFamily', new FormControl(this.user.preferences.bookReaderFontFamily, []));
        this.settingsForm.get('bookReaderFontFamily')!.valueChanges.pipe(takeUntil(this.onDestroy)).subscribe(fontName => {
          const familyName = this.fontFamilies.filter(f => f.title === fontName)[0].family;
          if (familyName === 'default') {
            this.pageStyles['font-family'] = 'inherit';
          } else {
            this.pageStyles['font-family'] = "'" + familyName + "'";
          }

          this.styleUpdate.emit(this.pageStyles);
        });

        this.settingsForm.addControl('bookReaderFontSize', new FormControl(this.user.preferences.bookReaderFontSize, []));
        this.settingsForm.get('bookReaderFontSize')?.valueChanges.pipe(takeUntil(this.onDestroy)).subscribe(value => {
          this.pageStyles['font-size'] = value + '%';
          this.styleUpdate.emit(this.pageStyles);
        });

        this.settingsForm.addControl('bookReaderTapToPaginate', new FormControl(this.user.preferences.bookReaderTapToPaginate, []));
        this.settingsForm.get('bookReaderTapToPaginate')?.valueChanges.pipe(takeUntil(this.onDestroy)).subscribe(value => {
          this.clickToPaginateChanged.emit(value);
        });

        this.settingsForm.addControl('bookReaderLineSpacing', new FormControl(this.user.preferences.bookReaderLineSpacing, []));
        this.settingsForm.get('bookReaderLineSpacing')?.valueChanges.pipe(takeUntil(this.onDestroy)).subscribe(value => {
          this.pageStyles['line-height'] = value + '%';
          this.styleUpdate.emit(this.pageStyles);
        });

        this.settingsForm.addControl('bookReaderMargin', new FormControl(this.user.preferences.bookReaderMargin, []));
        this.settingsForm.get('bookReaderMargin')?.valueChanges.pipe(takeUntil(this.onDestroy)).subscribe(value => {
          this.pageStyles['margin-left'] = value + 'vw';
          this.pageStyles['margin-right'] = value + 'vw';
          this.styleUpdate.emit(this.pageStyles);
        });



        this.settingsForm.addControl('layoutMode', new FormControl(this.user.preferences.bookReaderLayoutMode || BookPageLayoutMode.Default, []));
        this.settingsForm.get('layoutMode')?.valueChanges.pipe(takeUntil(this.onDestroy)).subscribe((layoutMode: BookPageLayoutMode) => {
          this.layoutModeUpdate.emit(layoutMode);
        });

        this.settingsForm.addControl('bookReaderImmersiveMode', new FormControl(this.user.preferences.bookReaderImmersiveMode, []));
        this.settingsForm.get('bookReaderImmersiveMode')?.valueChanges.pipe(takeUntil(this.onDestroy)).subscribe((immersiveMode: boolean) => {
          if (immersiveMode) {
            this.settingsForm.get('bookReaderTapToPaginate')?.setValue(true);
          }
          this.immersiveMode.emit(immersiveMode);
        });


        this.setTheme(this.user.preferences.bookReaderThemeName || this.themeService.defaultBookTheme);
        this.cdRef.markForCheck();

        // Emit first time so book reader gets the setting
        this.readingDirection.emit(this.readingDirectionModel);
        this.bookReaderReadingMode.emit(this.readingModeModel);
        this.clickToPaginateChanged.emit(this.user.preferences.bookReaderTapToPaginate);
        this.layoutModeUpdate.emit(this.user.preferences.bookReaderLayoutMode);
        this.immersiveMode.emit(this.user.preferences.bookReaderImmersiveMode);

        this.resetSettings();
      } else {
        this.resetSettings();
      }


    });
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }


  resetSettings() {
    if (this.user) {
      this.setPageStyles(this.user.preferences.bookReaderFontFamily, this.user.preferences.bookReaderFontSize + '%', this.user.preferences.bookReaderMargin + 'vw', this.user.preferences.bookReaderLineSpacing + '%');
    } else {
      this.setPageStyles();
    }

    this.settingsForm.get('bookReaderFontFamily')?.setValue(this.user.preferences.bookReaderFontFamily);
    this.cdRef.markForCheck();
    this.styleUpdate.emit(this.pageStyles);
  }

  /**
   * Internal method to be used by resetSettings. Pass items in with quantifiers
   */
  setPageStyles(fontFamily?: string, fontSize?: string, margin?: string, lineHeight?: string, colorTheme?: string) {
    const windowWidth = window.innerWidth
      || this.document.documentElement.clientWidth
      || this.document.body.clientWidth;


    let defaultMargin = '15vw';
    if (windowWidth <= mobileBreakpointMarginOverride) {
      defaultMargin = '5vw';
    }
    this.pageStyles = {
      'font-family': fontFamily || this.pageStyles['font-family'] || 'default',
      'font-size': fontSize || this.pageStyles['font-size'] || '100%',
      'margin-left': margin || this.pageStyles['margin-left']  || defaultMargin,
      'margin-right': margin || this.pageStyles['margin-right']  || defaultMargin,
      'line-height': lineHeight || this.pageStyles['line-height'] || '100%'
    };
  }

  setTheme(themeName: string) {
    const theme = this.themes.find(t => t.name === themeName);
    this.activeTheme = theme;
    this.cdRef.markForCheck();
    this.colorThemeUpdate.emit(theme);
  }

  toggleReadingDirection() {
    if (this.readingDirectionModel === ReadingDirection.LeftToRight) {
      this.readingDirectionModel = ReadingDirection.RightToLeft;
    } else {
      this.readingDirectionModel = ReadingDirection.LeftToRight;
    }

    this.cdRef.markForCheck();
    this.readingDirection.emit(this.readingDirectionModel);
  }

  toggleReadingMode() {
    if (this.readingModeModel === ReadingMode.Horizontally) {
      this.readingModeModel = ReadingMode.Vertically
    } else {
      this.readingModeModel = ReadingMode.Horizontally
    }

    this.cdRef.markForCheck();
    this.bookReaderReadingMode.emit(this.readingModeModel);
  }

  toggleFullscreen() {
    this.isFullscreen = !this.isFullscreen;
    this.cdRef.markForCheck();
    this.fullscreen.emit();
  }
}
