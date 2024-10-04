import { DOCUMENT, NgFor, NgTemplateOutlet, NgIf, NgClass, NgStyle, TitleCasePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  EventEmitter,
  inject,
  Inject,
  OnInit,
  Output
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { take } from 'rxjs';
import { BookPageLayoutMode } from 'src/app/_models/readers/book-page-layout-mode';
import { BookTheme } from 'src/app/_models/preferences/book-theme';
import { ReadingDirection } from 'src/app/_models/preferences/reading-direction';
import { WritingStyle } from 'src/app/_models/preferences/writing-style';
import { ThemeProvider } from 'src/app/_models/preferences/site-theme';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';
import { ThemeService } from 'src/app/_services/theme.service';
import { FontFamily, BookService } from '../../_services/book.service';
import { BookBlackTheme } from '../../_models/book-black-theme';
import { BookDarkTheme } from '../../_models/book-dark-theme';
import { BookWhiteTheme } from '../../_models/book-white-theme';
import { BookPaperTheme } from '../../_models/book-paper-theme';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { NgbAccordionDirective, NgbAccordionItem, NgbAccordionHeader, NgbAccordionToggle, NgbAccordionButton, NgbCollapse, NgbAccordionCollapse, NgbAccordionBody, NgbTooltip } from '@ng-bootstrap/ng-bootstrap';
import {TranslocoDirective} from "@jsverse/transloco";

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
    content: BookDarkTheme,
    translationKey: 'theme-dark'
  },
  {
    name: 'Black',
    colorHash: '#000000',
    isDarkTheme: true,
    isDefault: false,
    provider: ThemeProvider.System,
    selector: 'brtheme-black',
    content: BookBlackTheme,
    translationKey: 'theme-black'
  },
  {
    name: 'White',
    colorHash: '#FFFFFF',
    isDarkTheme: false,
    isDefault: false,
    provider: ThemeProvider.System,
    selector: 'brtheme-white',
    content: BookWhiteTheme,
    translationKey: 'theme-white'
  },
  {
    name: 'Paper',
    colorHash: '#F1E4D5',
    isDarkTheme: false,
    isDefault: false,
    provider: ThemeProvider.System,
    selector: 'brtheme-paper',
    content: BookPaperTheme,
    translationKey: 'theme-paper'
  },
];

const mobileBreakpointMarginOverride = 700;

@Component({
    selector: 'app-reader-settings',
    templateUrl: './reader-settings.component.html',
    styleUrls: ['./reader-settings.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [ReactiveFormsModule, NgbAccordionDirective, NgbAccordionItem, NgbAccordionHeader, NgbAccordionToggle, NgbAccordionButton, NgbCollapse, NgbAccordionCollapse, NgbAccordionBody, NgFor, NgbTooltip, NgTemplateOutlet, NgIf, NgClass, NgStyle, TitleCasePipe, TranslocoDirective]
})
export class ReaderSettingsComponent implements OnInit {
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
  @Output() bookReaderWritingStyle: EventEmitter<WritingStyle> = new EventEmitter();
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

  writingStyleModel: WritingStyle = WritingStyle.Horizontal;


  activeTheme: BookTheme | undefined;

  isFullscreen: boolean = false;

  settingsForm: FormGroup = new FormGroup({});

  /**
   * System provided themes
   */
  themes: Array<BookTheme> = bookColorThemes;
  private readonly destroyRef = inject(DestroyRef);


  get BookPageLayoutMode(): typeof BookPageLayoutMode  {
    return BookPageLayoutMode;
  }

  get ReadingDirection() {
    return ReadingDirection;
  }

  get WritingStyle() {
    return WritingStyle;
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
        if (this.user.preferences.bookReaderWritingStyle === undefined) {
          this.user.preferences.bookReaderWritingStyle = WritingStyle.Horizontal;
        }
        this.readingDirectionModel = this.user.preferences.bookReaderReadingDirection;
        this.writingStyleModel = this.user.preferences.bookReaderWritingStyle;



        this.settingsForm.addControl('bookReaderFontFamily', new FormControl(this.user.preferences.bookReaderFontFamily, []));
        this.settingsForm.get('bookReaderFontFamily')!.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(fontName => {
          const familyName = this.fontFamilies.filter(f => f.title === fontName)[0].family;
          if (familyName === 'default') {
            this.pageStyles['font-family'] = 'inherit';
          } else {
            this.pageStyles['font-family'] = "'" + familyName + "'";
          }

          this.styleUpdate.emit(this.pageStyles);
        });

        this.settingsForm.addControl('bookReaderFontSize', new FormControl(this.user.preferences.bookReaderFontSize, []));
        this.settingsForm.get('bookReaderFontSize')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(value => {
          this.pageStyles['font-size'] = value + '%';
          this.styleUpdate.emit(this.pageStyles);
        });

        this.settingsForm.addControl('bookReaderTapToPaginate', new FormControl(this.user.preferences.bookReaderTapToPaginate, []));
        this.settingsForm.get('bookReaderTapToPaginate')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(value => {
          this.clickToPaginateChanged.emit(value);
        });

        this.settingsForm.addControl('bookReaderLineSpacing', new FormControl(this.user.preferences.bookReaderLineSpacing, []));
        this.settingsForm.get('bookReaderLineSpacing')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(value => {
          this.pageStyles['line-height'] = value + '%';
          this.styleUpdate.emit(this.pageStyles);
        });

        this.settingsForm.addControl('bookReaderMargin', new FormControl(this.user.preferences.bookReaderMargin, []));
        this.settingsForm.get('bookReaderMargin')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(value => {
          this.pageStyles['margin-left'] = value + 'vw';
          this.pageStyles['margin-right'] = value + 'vw';
          this.styleUpdate.emit(this.pageStyles);
        });



        this.settingsForm.addControl('layoutMode', new FormControl(this.user.preferences.bookReaderLayoutMode || BookPageLayoutMode.Default, []));
        this.settingsForm.get('layoutMode')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((layoutMode: BookPageLayoutMode) => {
          this.layoutModeUpdate.emit(layoutMode);
        });

        this.settingsForm.addControl('bookReaderImmersiveMode', new FormControl(this.user.preferences.bookReaderImmersiveMode, []));
        this.settingsForm.get('bookReaderImmersiveMode')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((immersiveMode: boolean) => {
          if (immersiveMode) {
            this.settingsForm.get('bookReaderTapToPaginate')?.setValue(true);
          }
          this.immersiveMode.emit(immersiveMode);
        });


        this.setTheme(this.user.preferences.bookReaderThemeName || this.themeService.defaultBookTheme);
        this.cdRef.markForCheck();

        // Emit first time so book reader gets the setting
        this.readingDirection.emit(this.readingDirectionModel);
        this.bookReaderWritingStyle.emit(this.writingStyleModel);
        this.clickToPaginateChanged.emit(this.user.preferences.bookReaderTapToPaginate);
        this.layoutModeUpdate.emit(this.user.preferences.bookReaderLayoutMode);
        this.immersiveMode.emit(this.user.preferences.bookReaderImmersiveMode);

        this.resetSettings();
      } else {
        this.resetSettings();
      }


    });
  }

  resetSettings() {
    if (this.user) {
      this.setPageStyles(this.user.preferences.bookReaderFontFamily, this.user.preferences.bookReaderFontSize + '%', this.user.preferences.bookReaderMargin + 'vw', this.user.preferences.bookReaderLineSpacing + '%');
    } else {
      this.setPageStyles();
    }

    this.settingsForm.get('bookReaderFontFamily')?.setValue(this.user.preferences.bookReaderFontFamily);
    this.settingsForm.get('bookReaderFontSize')?.setValue(this.user.preferences.bookReaderFontSize);
    this.settingsForm.get('bookReaderLineSpacing')?.setValue(this.user.preferences.bookReaderLineSpacing);
    this.settingsForm.get('bookReaderMargin')?.setValue(this.user.preferences.bookReaderMargin);
    this.settingsForm.get('bookReaderReadingDirection')?.setValue(this.user.preferences.bookReaderReadingDirection);
    this.settingsForm.get('bookReaderTapToPaginate')?.setValue(this.user.preferences.bookReaderTapToPaginate);
    this.settingsForm.get('bookReaderLayoutMode')?.setValue(this.user.preferences.bookReaderLayoutMode);
    this.settingsForm.get('bookReaderImmersiveMode')?.setValue(this.user.preferences.bookReaderImmersiveMode);
    this.settingsForm.get('bookReaderWritingStyle')?.setValue(this.user.preferences.bookReaderWritingStyle);
    this.cdRef.detectChanges();
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

  toggleWritingStyle() {
    if (this.writingStyleModel === WritingStyle.Horizontal) {
      this.writingStyleModel = WritingStyle.Vertical
    } else {
      this.writingStyleModel = WritingStyle.Horizontal
    }

    this.cdRef.markForCheck();
    this.bookReaderWritingStyle.emit(this.writingStyleModel);
  }

  toggleFullscreen() {
    this.isFullscreen = !this.isFullscreen;
    this.cdRef.markForCheck();
    this.fullscreen.emit();
  }
}
