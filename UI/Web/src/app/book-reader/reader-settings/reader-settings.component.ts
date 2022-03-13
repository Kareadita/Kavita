import { DOCUMENT } from '@angular/common';
import { Component, EventEmitter, Inject, OnDestroy, OnInit, Output } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { Subject, take, takeUntil } from 'rxjs';
import { ThemeService } from 'src/app/theme.service';
import { BookTheme } from 'src/app/_models/preferences/book-theme';
import { ReadingDirection } from 'src/app/_models/preferences/reading-direction';
import { ThemeProvider } from 'src/app/_models/preferences/site-theme';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';
import { BookService } from '../book.service';
import { BookBlackTheme } from '../_models/book-black-theme';
import { BookDarkTheme } from '../_models/book-dark-theme';
import { BookWhiteTheme } from '../_models/book-white-theme';

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

export enum LayoutMode {
  Default = 'Default',
  Column2 = '2 Column',
  Column1 = 'Column'
}

const mobileBreakpointMarginOverride = 700;

@Component({
  selector: 'app-reader-settings',
  templateUrl: './reader-settings.component.html',
  styleUrls: ['./reader-settings.component.scss']
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
  @Output() layoutModeUpdate: EventEmitter<LayoutMode> = new EventEmitter();
  /**
   * Outputs when fullscreen is toggled
   */
  @Output() fullscreen: EventEmitter<void> = new EventEmitter();
  
  user!: User;
  /**
   * List of all font families user can select from
   */
  fontFamilies: Array<string> = [];
  /**
   * Internal property used to capture all the different css properties to render on all elements
   */
  pageStyles!: PageStyle;

  readingDirection: ReadingDirection = ReadingDirection.LeftToRight;

  activeTheme: BookTheme | undefined;

  isFullscreen: boolean = false;

  settingsForm: FormGroup = new FormGroup({});

  /**
   * System provided themes
   */
  themes: Array<BookTheme> = [
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



  private onDestroy: Subject<void> = new Subject();


  get LayoutMode(): typeof LayoutMode  {
    return LayoutMode;
  }



  constructor(private bookService: BookService, private accountService: AccountService, @Inject(DOCUMENT) private document: Document, private themeService: ThemeService) {}

  ngOnInit(): void {
    
    this.fontFamilies = this.bookService.getFontFamilies();

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


          this.readingDirection = this.user.preferences.bookReaderReadingDirection;
          this.settingsForm.addControl('bookReaderReadingDirection', new FormControl(this.user.preferences.bookReaderReadingDirection, []));
          this.settingsForm.get('bookReaderReadingDirection')?.valueChanges.pipe(takeUntil(this.onDestroy)).subscribe(value => {
            // TODO: Figure out what to do
            this.readingDirection = value;
          });

          
          this.settingsForm.addControl('bookReaderFontFamily', new FormControl(this.user.preferences.bookReaderFontFamily, []));
          this.settingsForm.get('bookReaderFontFamily')!.valueChanges.pipe(takeUntil(this.onDestroy)).subscribe(familyName => {
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
            this.pageStyles['margin-left'] = value + '%';
            this.pageStyles['margin-right'] = value + '%';
            this.styleUpdate.emit(this.pageStyles);
          });

          this.settingsForm.addControl('layoutMode', new FormControl(LayoutMode.Default, []));//this.user.preferences.layoutMode || 
          this.settingsForm.get('layoutMode')?.valueChanges.pipe(takeUntil(this.onDestroy)).subscribe((layoutMode: LayoutMode) => {
            this.layoutModeUpdate.emit(layoutMode);
          });

          this.setTheme(this.user.preferences.bookReaderThemeName || this.themeService.defaultBookTheme);
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
      this.setPageStyles(this.user.preferences.bookReaderFontFamily, this.user.preferences.bookReaderFontSize + '%', this.user.preferences.bookReaderMargin + '%', this.user.preferences.bookReaderLineSpacing + '%');
    } else {
      this.setPageStyles();
    }
    
    this.settingsForm.get('bookReaderFontFamily')?.setValue(this.user.preferences.bookReaderFontFamily);
    this.styleUpdate.emit(this.pageStyles);
  }

  /**
   * Internal method to be used by resetSettings. Pass items in with quantifiers
   */
  setPageStyles(fontFamily?: string, fontSize?: string, margin?: string, lineHeight?: string, colorTheme?: string) {
    const windowWidth = window.innerWidth
      || this.document.documentElement.clientWidth
      || this.document.body.clientWidth;
      

    let defaultMargin = '15%';
    if (windowWidth <= mobileBreakpointMarginOverride) {
      defaultMargin = '5%';
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
    this.colorThemeUpdate.emit(theme);
  }

  toggleReadingDirection() {
    if (this.readingDirection === ReadingDirection.LeftToRight) {
      this.readingDirection = ReadingDirection.RightToLeft;
    } else {
      this.readingDirection = ReadingDirection.LeftToRight;
    }
  }

  toggleFullscreen() {
    this.fullscreen.emit();
  }
}
