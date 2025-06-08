import {DOCUMENT, NgClass, NgFor, NgIf, NgStyle, NgTemplateOutlet, TitleCasePipe} from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  EventEmitter,
  inject,
  Inject,
  Input,
  OnInit,
  Output
} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {skip, take} from 'rxjs';
import {BookPageLayoutMode} from 'src/app/_models/readers/book-page-layout-mode';
import {BookTheme} from 'src/app/_models/preferences/book-theme';
import {ReadingDirection} from 'src/app/_models/preferences/reading-direction';
import {WritingStyle} from 'src/app/_models/preferences/writing-style';
import {ThemeProvider} from 'src/app/_models/preferences/site-theme';
import {User} from 'src/app/_models/user';
import {AccountService} from 'src/app/_services/account.service';
import {ThemeService} from 'src/app/_services/theme.service';
import {BookService, FontFamily} from '../../_services/book.service';
import {BookBlackTheme} from '../../_models/book-black-theme';
import {BookDarkTheme} from '../../_models/book-dark-theme';
import {BookWhiteTheme} from '../../_models/book-white-theme';
import {BookPaperTheme} from '../../_models/book-paper-theme';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {
  NgbAccordionBody,
  NgbAccordionButton,
  NgbAccordionCollapse,
  NgbAccordionDirective,
  NgbAccordionHeader,
  NgbAccordionItem,
  NgbTooltip
} from '@ng-bootstrap/ng-bootstrap';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ReadingProfileService} from "../../../_services/reading-profile.service";
import {ReadingProfile, ReadingProfileKind} from "../../../_models/preferences/reading-profiles";
import {debounceTime, distinctUntilChanged, tap} from "rxjs/operators";
import {ToastrService} from "ngx-toastr";

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
    imports: [ReactiveFormsModule, NgbAccordionDirective, NgbAccordionItem, NgbAccordionHeader, NgbAccordionButton,
      NgbAccordionCollapse, NgbAccordionBody, NgFor, NgbTooltip, NgTemplateOutlet, NgIf, NgClass, NgStyle,
      TitleCasePipe, TranslocoDirective]
})
export class ReaderSettingsComponent implements OnInit {
  @Input({required:true}) seriesId!: number;
  @Input({required:true}) readingProfile!: ReadingProfile;
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
   * The reading profile itself, unless readingProfile is implicit
   */
  parentReadingProfile: ReadingProfile | null = null;

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
    private readonly cdRef: ChangeDetectorRef, private readingProfileService: ReadingProfileService,
              private toastr: ToastrService) {}

  ngOnInit(): void {
    if (this.readingProfile.kind === ReadingProfileKind.Implicit) {
      this.readingProfileService.getForSeries(this.seriesId, true).subscribe(parent => {
        this.parentReadingProfile = parent;
        this.cdRef.markForCheck();
      })
    } else {
      this.parentReadingProfile = this.readingProfile;
      this.cdRef.markForCheck();
    }

    this.fontFamilies = this.bookService.getFontFamilies();
    this.fontOptions = this.fontFamilies.map(f => f.title);



    this.cdRef.markForCheck();

    this.setupSettings();

    this.setTheme(this.readingProfile.bookReaderThemeName || this.themeService.defaultBookTheme, false);
    this.cdRef.markForCheck();

    // Emit first time so book reader gets the setting
    this.readingDirection.emit(this.readingDirectionModel);
    this.bookReaderWritingStyle.emit(this.writingStyleModel);
    this.clickToPaginateChanged.emit(this.readingProfile.bookReaderTapToPaginate);
    this.layoutModeUpdate.emit(this.readingProfile.bookReaderLayoutMode);
    this.immersiveMode.emit(this.readingProfile.bookReaderImmersiveMode);

    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.user = user;
      }

      // User needs to be loaded before we call this
      this.resetSettings();
    });
  }

  setupSettings() {
    if (!this.readingProfile) return;

    if (this.readingProfile.bookReaderFontFamily === undefined) {
      this.readingProfile.bookReaderFontFamily = 'default';
    }
    if (this.readingProfile.bookReaderFontSize === undefined || this.readingProfile.bookReaderFontSize < 50) {
      this.readingProfile.bookReaderFontSize = 100;
    }
    if (this.readingProfile.bookReaderLineSpacing === undefined || this.readingProfile.bookReaderLineSpacing < 100) {
      this.readingProfile.bookReaderLineSpacing = 100;
    }
    if (this.readingProfile.bookReaderMargin === undefined) {
      this.readingProfile.bookReaderMargin = 0;
    }
    if (this.readingProfile.bookReaderReadingDirection === undefined) {
      this.readingProfile.bookReaderReadingDirection = ReadingDirection.LeftToRight;
    }
    if (this.readingProfile.bookReaderWritingStyle === undefined) {
      this.readingProfile.bookReaderWritingStyle = WritingStyle.Horizontal;
    }
    this.readingDirectionModel = this.readingProfile.bookReaderReadingDirection;
    this.writingStyleModel = this.readingProfile.bookReaderWritingStyle;

    this.settingsForm.addControl('bookReaderFontFamily', new FormControl(this.readingProfile.bookReaderFontFamily, []));
    this.settingsForm.get('bookReaderFontFamily')!.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(fontName => {
      const familyName = this.fontFamilies.filter(f => f.title === fontName)[0].family;
      if (familyName === 'default') {
        this.pageStyles['font-family'] = 'inherit';
      } else {
        this.pageStyles['font-family'] = "'" + familyName + "'";
      }

      this.styleUpdate.emit(this.pageStyles);
    });

    this.settingsForm.addControl('bookReaderFontSize', new FormControl(this.readingProfile.bookReaderFontSize, []));
    this.settingsForm.get('bookReaderFontSize')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(value => {
      this.pageStyles['font-size'] = value + '%';
      this.styleUpdate.emit(this.pageStyles);
    });

    this.settingsForm.addControl('bookReaderTapToPaginate', new FormControl(this.readingProfile.bookReaderTapToPaginate, []));
    this.settingsForm.get('bookReaderTapToPaginate')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(value => {
      this.clickToPaginateChanged.emit(value);
    });

    this.settingsForm.addControl('bookReaderLineSpacing', new FormControl(this.readingProfile.bookReaderLineSpacing, []));
    this.settingsForm.get('bookReaderLineSpacing')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(value => {
      this.pageStyles['line-height'] = value + '%';
      this.styleUpdate.emit(this.pageStyles);
    });

    this.settingsForm.addControl('bookReaderMargin', new FormControl(this.readingProfile.bookReaderMargin, []));
    this.settingsForm.get('bookReaderMargin')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(value => {
      this.pageStyles['margin-left'] = value + 'vw';
      this.pageStyles['margin-right'] = value + 'vw';
      this.styleUpdate.emit(this.pageStyles);
    });

    this.settingsForm.addControl('layoutMode', new FormControl(this.readingProfile.bookReaderLayoutMode, []));
    this.settingsForm.get('layoutMode')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((layoutMode: BookPageLayoutMode) => {
      this.layoutModeUpdate.emit(layoutMode);
    });

    this.settingsForm.addControl('bookReaderImmersiveMode', new FormControl(this.readingProfile.bookReaderImmersiveMode, []));
    this.settingsForm.get('bookReaderImmersiveMode')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((immersiveMode: boolean) => {
      if (immersiveMode) {
        this.settingsForm.get('bookReaderTapToPaginate')?.setValue(true);
      }
      this.immersiveMode.emit(immersiveMode);
    });

    // Update implicit reading profile while changing settings
    this.settingsForm.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      skip(1), // Skip the initial creation of the form, we do not want an implicit profile of this snapshot
      takeUntilDestroyed(this.destroyRef),
      tap(_ => this.updateImplicit())
    ).subscribe();
  }

  resetSettings() {
    if (!this.readingProfile) return;

    if (this.user) {
      this.setPageStyles(this.readingProfile.bookReaderFontFamily, this.readingProfile.bookReaderFontSize + '%', this.readingProfile.bookReaderMargin + 'vw', this.readingProfile.bookReaderLineSpacing + '%');
    } else {
      this.setPageStyles();
    }

    this.settingsForm.get('bookReaderFontFamily')?.setValue(this.readingProfile.bookReaderFontFamily);
    this.settingsForm.get('bookReaderFontSize')?.setValue(this.readingProfile.bookReaderFontSize);
    this.settingsForm.get('bookReaderLineSpacing')?.setValue(this.readingProfile.bookReaderLineSpacing);
    this.settingsForm.get('bookReaderMargin')?.setValue(this.readingProfile.bookReaderMargin);
    this.settingsForm.get('bookReaderReadingDirection')?.setValue(this.readingProfile.bookReaderReadingDirection);
    this.settingsForm.get('bookReaderTapToPaginate')?.setValue(this.readingProfile.bookReaderTapToPaginate);
    this.settingsForm.get('bookReaderLayoutMode')?.setValue(this.readingProfile.bookReaderLayoutMode);
    this.settingsForm.get('bookReaderImmersiveMode')?.setValue(this.readingProfile.bookReaderImmersiveMode);
    this.settingsForm.get('bookReaderWritingStyle')?.setValue(this.readingProfile.bookReaderWritingStyle);

    this.cdRef.detectChanges();
    this.styleUpdate.emit(this.pageStyles);
  }

  updateImplicit() {
    this.readingProfileService.updateImplicit(this.packReadingProfile(), this.seriesId).subscribe({
      next: newProfile => {
        this.readingProfile = newProfile;
        this.cdRef.markForCheck();
      },
      error: err => {
        console.error(err);
      }
    })
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

  setTheme(themeName: string, update: boolean = true) {
    const theme = this.themes.find(t => t.name === themeName);
    this.activeTheme = theme;
    this.cdRef.markForCheck();
    this.colorThemeUpdate.emit(theme);

    if (update) {
      this.updateImplicit();
    }
  }

  toggleReadingDirection() {
    if (this.readingDirectionModel === ReadingDirection.LeftToRight) {
      this.readingDirectionModel = ReadingDirection.RightToLeft;
    } else {
      this.readingDirectionModel = ReadingDirection.LeftToRight;
    }

    this.cdRef.markForCheck();
    this.readingDirection.emit(this.readingDirectionModel);
    this.updateImplicit();
  }

  toggleWritingStyle() {
    if (this.writingStyleModel === WritingStyle.Horizontal) {
      this.writingStyleModel = WritingStyle.Vertical
    } else {
      this.writingStyleModel = WritingStyle.Horizontal
    }

    this.cdRef.markForCheck();
    this.bookReaderWritingStyle.emit(this.writingStyleModel);
    this.updateImplicit();
  }

  toggleFullscreen() {
    this.isFullscreen = !this.isFullscreen;
    this.cdRef.markForCheck();
    this.fullscreen.emit();
  }

  // menu only code
  updateParentPref() {
    if (this.readingProfile.kind !== ReadingProfileKind.Implicit) {
      return;
    }

    this.readingProfileService.updateParentProfile(this.seriesId, this.packReadingProfile()).subscribe(newProfile => {
      this.readingProfile = newProfile;
      this.toastr.success(translate('manga-reader.reading-profile-updated'));
      this.cdRef.markForCheck();
    });
  }

  createNewProfileFromImplicit() {
    if (this.readingProfile.kind !== ReadingProfileKind.Implicit) {
      return;
    }

    this.readingProfileService.promoteProfile(this.readingProfile.id).subscribe(newProfile => {
      this.readingProfile = newProfile;
      this.parentReadingProfile = newProfile; // profile is no longer implicit
      this.cdRef.markForCheck();

      this.toastr.success(translate("manga-reader.reading-profile-promoted"));
    });
  }

  private packReadingProfile(): ReadingProfile {
    const modelSettings = this.settingsForm.getRawValue();
    const data = {...this.readingProfile!};
    data.bookReaderFontFamily = modelSettings.bookReaderFontFamily;
    data.bookReaderFontSize = modelSettings.bookReaderFontSize
    data.bookReaderLineSpacing = modelSettings.bookReaderLineSpacing;
    data.bookReaderMargin = modelSettings.bookReaderMargin;
    data.bookReaderTapToPaginate = modelSettings.bookReaderTapToPaginate;
    data.bookReaderLayoutMode = modelSettings.layoutMode;
    data.bookReaderImmersiveMode = modelSettings.bookReaderImmersiveMode;

    data.bookReaderReadingDirection = this.readingDirectionModel;
    data.bookReaderWritingStyle = this.writingStyleModel;
    if (this.activeTheme) {
      data.bookReaderThemeName = this.activeTheme.name;
    }

    return data;
  }

  protected readonly ReadingProfileKind = ReadingProfileKind;
}
