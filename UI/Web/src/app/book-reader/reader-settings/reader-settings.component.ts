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

// Important note about themes. Must have one section with .reader-container that contains color, background-color and rest of the styles must be scoped to .book-content
const BookDarkTheme = `
:root() .brtheme-dark {
  --accordion-body-bg-color: black;
  --accordion-header-bg-color: grey;

}

.book-content *:not(input), .book-content *:not(select), .book-content *:not(code), .book-content *:not(:link), .book-content *:not(.ngx-toastr) {
  color: #dcdcdc !important;
}

.book-content code {
  color: #e83e8c !important;
}

.book-content :link, .book-content a {
  color: #8db2e5 !important;
}

.book-content img, .book-content img[src] {
z-index: 1;
filter: brightness(0.85) !important;
background-color: initial !important;
}

.reader-container {
  color: #dcdcdc !important;
  background-image: none !important;
  background-color: #292929 !important;
}

.book-content *:not(code), .book-content *:not(a) {
    background-color: #292929;
    box-shadow: none;
    text-shadow: none;
    border-radius: unset;
    color: #dcdcdc !important;
}
  
.book-content :visited, .book-content :visited *, .book-content :visited *[class] {color: rgb(211, 138, 138) !important}
.book-content :link:not(cite), :link .book-content *:not(cite) {color: #8db2e5 !important}
`;

const BookBlackTheme = `
.reader-container {
  color: #dcdcdc !important;
  background-image: none !important;
  background-color: #010409 !important;
}

.book-content *:not(input), .book-content *:not(select), .book-content *:not(code), .book-content *:not(:link), .book-content *:not(.ngx-toastr) {
  color: #dcdcdc !important;
}

.book-content code {
  color: #e83e8c !important;
}

.book-content :link, .book-content a {
  color: #8db2e5 !important;
}

.book-content img, .book-content img[src] {
z-index: 1;
filter: brightness(0.85) !important;
background-color: initial !important;
}

.book-content *:not(code), .book-content *:not(a) {
    background-color: #010409;
    box-shadow: none;
    text-shadow: none;
    border-radius: unset;
    color: #dcdcdc !important;
}

.book-content *:not(input), .book-content *:not(code), .book-content *:not(:link) {
    color: #dcdcdc !important;
}

.book-content :visited, .book-content :visited *, .book-content :visited *[class] {color: rgb(211, 138, 138) !important}
.book-content :link:not(cite), :link .book-content *:not(cite) {color: #8db2e5 !important}
`;

const BookWhiteTheme = `
  :root() .brtheme-white {
    --brtheme-link-text-color: green;
    --brtheme-bg-color: lightgrey;
  }
`;

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
  /**
   * Dark mode for reader. Will be replaced with custom theme.
   * @deprecated Use themes instead
   */
  darkMode: boolean = true;

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
