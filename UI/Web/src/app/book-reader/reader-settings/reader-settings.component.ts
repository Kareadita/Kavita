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
      selector: 'brtheme-dark'
    },
    {
      name: 'Black',
      colorHash: '#000000',
      isDarkTheme: true,
      isDefault: false,
      provider: ThemeProvider.System,
      selector: 'brtheme-black'
    },
    {
      name: 'White',
      colorHash: '#FFFFFF',
      isDarkTheme: false,
      isDefault: false,
      provider: ThemeProvider.System,
      selector: 'brtheme-white'
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
        }

        this.resetSettings();
      });
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }


  // TODO: Refactor this so that we first reset the form to user's settings then rebuild pageStyles
  resetSettings() {
    const windowWidth = window.innerWidth
      || this.document.documentElement.clientWidth
      || this.document.body.clientWidth;

    let margin = '15%';
    if (windowWidth <= 700) {
      margin = '5%';
    }
    if (this.user) {
      if (windowWidth > 700) {
        margin = this.user.preferences.bookReaderMargin + '%';
      }
      this.pageStyles = {'font-family': this.user.preferences.bookReaderFontFamily, 'font-size': this.user.preferences.bookReaderFontSize + '%', 
      'margin-left': margin, 'margin-right': margin, 'line-height': this.user.preferences.bookReaderLineSpacing + '%'};
      console.log('line spacing: ', this.user.preferences.bookReaderLineSpacing);
      
      //this.toggleDarkMode(this.user.preferences.bookReaderDarkMode);
      this.setTheme(this.user.preferences.bookReaderThemeName || this.themeService.defaultBookTheme);
    } else {
      this.pageStyles = {'font-family': 'default', 'font-size': '100%', 'margin-left': margin, 'margin-right': margin, 'line-height': '100%'};
      //this.toggleDarkMode(false);
      this.setTheme(this.themeService.defaultBookTheme);
    }
    
    this.settingsForm.get('bookReaderFontFamily')?.setValue(this.user.preferences.bookReaderFontFamily);
    this.styleUpdate.emit(this.pageStyles);
  }

  setTheme(themeName: string) {
    const theme = this.themes.find(t => t.name == themeName);
    this.colorThemeUpdate.emit(theme);
  }

  // toggleDarkMode(force?: boolean) {
  //   let theme = this.themes[0];
  //   if (force !== undefined) {
  //     this.darkMode = force;
  //   } else {
  //     this.darkMode = !this.darkMode;
  //   }
  //   if (this.darkMode) {
  //     theme = this.themes.filter(t => t.name === 'Dark')[0];
  //   } else {
  //     theme = this.themes.filter(t => t.name === 'White')[0];
  //   }

  //   this.colorThemeUpdate.emit({theme, 'darkMode': this.darkMode});
  // }

  toggleReadingDirection() {
    if (this.readingDirection === ReadingDirection.LeftToRight) {
      this.readingDirection = ReadingDirection.RightToLeft;
    } else {
      this.readingDirection = ReadingDirection.LeftToRight;
    }
  }

  toggleFullscreen() {
    // TODO: Emit event so main reader can handle
    this.fullscreen.emit();
    // this.isFullscreen = this.readerService.checkFullscreenMode();
    
    // if (this.isFullscreen) {
    //   this.readerService.exitFullscreen(() => {
    //     this.isFullscreen = false;
    //     this.renderer.removeStyle(this.reader.nativeElement, 'background');
    //   });
    // } else {
    //   this.readerService.enterFullscreen(this.reader.nativeElement, () => {
    //     this.isFullscreen = true;
    //     // HACK: This is a bug with how browsers change the background color for fullscreen mode
    //     if (!this.darkMode) {
    //       this.renderer.setStyle(this.reader.nativeElement, 'background', 'white');
    //     }
    //   });
    // }
  }



}
