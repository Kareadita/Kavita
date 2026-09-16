import {computed, DestroyRef, effect, inject, Injectable, signal, untracked} from '@angular/core';
import {firstValueFrom, Observable, skip, Subject} from 'rxjs';
import {bookColorThemes, PageStyle} from "../book-reader/_components/reader-settings/reader-settings.component";
import {ReadingDirection} from '../_models/preferences/reading-direction';
import {WritingStyle} from '../_models/preferences/writing-style';
import {BookPageLayoutMode} from "../_models/readers/book-page-layout-mode";
import {FormControl, FormGroup, NonNullableFormBuilder} from "@angular/forms";
import {ReadingProfile, ReadingProfileKind} from "../_models/preferences/reading-profiles";
import {ThemeService} from './theme.service';
import {ReadingProfileService} from "./reading-profile.service";
import {debounceTime, distinctUntilChanged, filter, tap} from "rxjs/operators";
import {BookTheme} from "../_models/preferences/book-theme";
import {DOCUMENT} from "@angular/common";
import {translate} from "@jsverse/transloco";
import {ToastrService} from '@openng/ngx-toastr';
import {takeUntilDestroyed, toObservable} from '@angular/core/rxjs-interop';
import {UtilityService} from "../shared/_services/utility.service";
import {environment} from "../../environments/environment";
import {EpubFont, FontProvider} from "../_models/preferences/epub-font";
import {FontService} from "./font.service";
import {BreakpointService} from "./breakpoint.service";
import {FieldTree, form, max, min} from "@angular/forms/signals";

export interface ReaderSettingUpdate {
  setting: 'pageStyle' | 'clickToPaginate' | 'fullscreen' | 'writingStyle' | 'layoutMode' | 'readingDirection' | 'immersiveMode' | 'theme' | 'pageCalcMethod' | 'bookReaderDisableBookmarkIcon';
  object: any;
}

export interface BookReadingProfileFormModel {
  bookReaderMargin: number;
  bookReaderLineSpacing: number;
  bookReaderFontSize: number;
  bookReaderFontFamily: string;
  bookReaderTapToPaginate: boolean;
  bookReaderReadingDirection: ReadingDirection;
  bookReaderWritingStyle: WritingStyle;
  bookReaderThemeName: string;
  bookReaderLayoutMode: BookPageLayoutMode;
  bookReaderImmersiveMode: boolean;
  bookReaderDisableBookmarkIcon: boolean;
}

@Injectable()
export class EpubReaderSettingsService {

  private readonly fontService = inject(FontService);
  private readonly themeService = inject(ThemeService);
  private readonly readingProfileService = inject(ReadingProfileService);
  private readonly toastr = inject(ToastrService);
  private readonly document = inject(DOCUMENT);
  protected readonly breakpointService = inject(BreakpointService);

  //#region State

  // Core signals - these will be the single source of truth
  private readonly _currentReadingProfile = signal<ReadingProfile | null>(null);
  private readonly _parentReadingProfile = signal<ReadingProfile | null>(null);
  private readonly _currentSeriesId = signal<number | null>(null);
  private readonly _currentLibraryId = signal<number | null>(null);
  private readonly _isInitialized = signal<boolean>(false);
  private readonly _epubFonts = signal<EpubFont[]>([]);
  private readonly _activeTheme = signal<BookTheme | undefined>(undefined);
  private readonly _isFullscreen = signal<boolean>(false);

  formModel = signal<BookReadingProfileFormModel>({
    bookReaderDisableBookmarkIcon: false,
    bookReaderFontFamily: "",
    bookReaderFontSize: 0,
    bookReaderImmersiveMode: false,
    bookReaderLayoutMode: BookPageLayoutMode.Default,
    bookReaderLineSpacing: 0,
    bookReaderMargin: 0,
    bookReaderReadingDirection: ReadingDirection.LeftToRight,
    bookReaderTapToPaginate: false,
    bookReaderThemeName: "",
    bookReaderWritingStyle: WritingStyle.Horizontal
  })
  formGroup = form(this.formModel, path => {
    min(path.bookReaderFontSize, 50);
    max(path.bookReaderFontSize, 300);
    min(path.bookReaderLineSpacing, 100);
    max(path.bookReaderLineSpacing, 200);
    min(path.bookReaderMargin, 0);
    max(path.bookReaderMargin, 30);
  });

  // Public readonly signals
  public readonly currentReadingProfile = this._currentReadingProfile.asReadonly();
  public readonly parentReadingProfile = this._parentReadingProfile.asReadonly();

  // Settings as readonly signals
  public readonly pageStyles = computed<PageStyle>(() => {
    const fontFamily = this.formGroup.bookReaderFontFamily().value();
    const fontSize = this.formGroup.bookReaderFontSize().value();
    const lineSpacing = this.formGroup.bookReaderLineSpacing().value();
    const margin = this.formGroup.bookReaderMargin().value();

    return {
      'font-family': this.toCssFontFamily(fontFamily),
      'font-size': `${fontSize}%`,
      'line-height': `${lineSpacing}%`,
      'margin-left': `${margin}vw`,
      'margin-right': `${margin}vw`,
    };
  });
  public readonly readingDirection = computed(() => this.formGroup.bookReaderReadingDirection().value());
  public readonly writingStyle = computed(() => this.formGroup.bookReaderWritingStyle().value());
  public readonly activeTheme = this._activeTheme.asReadonly();
  public readonly clickToPaginate = computed(() => this.formGroup.bookReaderTapToPaginate().value());
  public readonly immersiveMode = computed(() => this.formGroup.bookReaderImmersiveMode().value());
  public readonly isFullscreen = this._isFullscreen.asReadonly();
  public readonly epubFonts = this._epubFonts.asReadonly();

  // Computed signals for derived state
  public readonly layoutMode = computed(() => {
    const layout = this.formGroup.bookReaderLayoutMode().value();
    const mobileDevice = this.breakpointService.isMobile();

    if (layout !== BookPageLayoutMode.Column2 || !mobileDevice) return layout;

    // Do not use 2 column mode on small screens
    this.toastr.info(translate('book-reader.force-selected-one-column'));
    return BookPageLayoutMode.Column1;
  });

  public readonly canPromoteProfile = computed(() => {
    const profile = this._currentReadingProfile();
    return profile !== null && profile.kind === ReadingProfileKind.Implicit;
  });

  public readonly hasParentProfile = computed(() => {
    return this._parentReadingProfile() !== null;
  });

  //#endregion

  constructor() {
    effect(() => {
      const layoutMode = this.layoutMode();

      if (untracked(this.writingStyle) === WritingStyle.Vertical && layoutMode == BookPageLayoutMode.Column2) {
        this.toastr.info(translate('book-reader.forced-vertical-switch'));
      }
    });

    effect(() => {
      const immersiveMode = this.immersiveMode();

      if (immersiveMode && !untracked(this.clickToPaginate)) {
        this.formGroup.bookReaderTapToPaginate().value.set(true);
      }
    });

    toObservable(this.formGroup().value).pipe(
      debounceTime(500),
      distinctUntilChanged(),
      skip(1), // Data loaded
      tap(() => this.updateImplicitProfile()),
    ).subscribe();
  }

  /**
   * Initialize the service with a reading profile and series ID
   */
  async initialize(libraryId: number, seriesId: number, readingProfile: ReadingProfile): Promise<void> {
    const fonts = await firstValueFrom(this.fontService.getFonts());
    this._epubFonts.set([...new Map(fonts.map(font => [`${font.family}`, font])).values()]);

    this._currentSeriesId.set(seriesId);
    this._currentLibraryId.set(libraryId);
    this._currentReadingProfile.set(readingProfile);

    // Load parent profile if needed, otherwise profile is its own parent
    if (readingProfile.kind === ReadingProfileKind.Implicit) {
      try {
        const parent = await firstValueFrom(this.readingProfileService.getForSeries(libraryId, seriesId, true));
        this._parentReadingProfile.set(parent || null);
      } catch (error) {
        console.error('Failed to load parent reading profile:', error);
      }
    } else {
      this._parentReadingProfile.set(readingProfile);
    }

    // Setup defaults and update signals
    this.correctInvalidReadingProfileOptions(readingProfile);
    this.setFormModelFromReadingProfile();

    // Set initial theme
    const themeName = readingProfile.bookReaderThemeName || this.themeService.defaultBookTheme;
    this.setTheme(themeName, false);

    // Mark as initialized - this will trigger effects to emit initial values
    this._isInitialized.set(true);
  }

  /**
   * Correct invalid values in a reading profile
   */
  private correctInvalidReadingProfileOptions(profile: ReadingProfile): void {
    // Set defaults if undefined
    if (profile.bookReaderFontFamily === undefined || profile.bookReaderFontFamily === 'default') {
      profile.bookReaderFontFamily = FontService.DefaultEpubFont;
    }
    if (profile.bookReaderFontSize === undefined || profile.bookReaderFontSize < 50) {
      profile.bookReaderFontSize = 100;
    }
    if (profile.bookReaderLineSpacing === undefined || profile.bookReaderLineSpacing < 100) {
      profile.bookReaderLineSpacing = 100;
    }
    if (profile.bookReaderMargin === undefined) {
      profile.bookReaderMargin = 0;
    }
    if (profile.bookReaderReadingDirection === undefined) {
      profile.bookReaderReadingDirection = ReadingDirection.LeftToRight;
    }
    if (profile.bookReaderWritingStyle === undefined) {
      profile.bookReaderWritingStyle = WritingStyle.Horizontal;
    }
    if (profile.bookReaderLayoutMode === undefined) {
      profile.bookReaderLayoutMode = BookPageLayoutMode.Default;
    }
  }

  /**
   * Get the current settings form (for components that need direct form access)
   */
  getSettingsForm(): FieldTree<BookReadingProfileFormModel> {
    return this.formGroup;
  }

  /**
   * Get current reading profile
   */
  getCurrentReadingProfile(): ReadingProfile | null {
    return this._currentReadingProfile();
  }

  /**
   * Get available themes
   */
  getThemes(): BookTheme[] {
    return bookColorThemes;
  }

  /**
   * Toggle reading direction
   */
  toggleReadingDirection(): void {
    const current = this.readingDirection();
    const newDirection = current === ReadingDirection.LeftToRight
      ? ReadingDirection.RightToLeft
      : ReadingDirection.LeftToRight;

    this.formGroup.bookReaderReadingDirection().value.set(newDirection);
  }

  /**
   * Toggle writing style
   */
  toggleWritingStyle(): void {
    const current = this.writingStyle();
    const newStyle = current === WritingStyle.Horizontal
      ? WritingStyle.Vertical
      : WritingStyle.Horizontal;

    // Default back to Col 1 in this case
    if (newStyle === WritingStyle.Vertical ) {
      if (this.layoutMode() === BookPageLayoutMode.Column2) {
        this.updateLayoutMode(BookPageLayoutMode.Column1);
      }
    }

    this.formGroup.bookReaderWritingStyle().value.set(newStyle);
  }

  /**
   * Set theme
   */
  setTheme(themeName: string, update: boolean = true): void {
    const theme = bookColorThemes.find(t => t.name === themeName);
    if (theme) {
      this._activeTheme.set(theme);
      if (update) {
        this.formGroup.bookReaderThemeName().value.set(theme.name);
      }
    }
  }

  updateLayoutMode(mode: BookPageLayoutMode): void {
    this.formGroup.bookReaderLayoutMode().value.set(mode);
  }

  updateClickToPaginate(value: boolean): void {
    this.formGroup.bookReaderTapToPaginate().value.set(value);
  }

  updateReadingDirection(value: ReadingDirection): void {
    this.formGroup.bookReaderReadingDirection().value.set(value);
  }

  updateWritingStyle(value: WritingStyle) {
    this.formGroup.bookReaderWritingStyle().value.set(value);
  }

  updateFullscreen(value: boolean) {
    this._isFullscreen.set(value);
    if (!this._isInitialized()) return;
  }

  updateImmersiveMode(value: boolean): void {
    this.formGroup.bookReaderImmersiveMode().value.set(value);
    if (value) {
      this.formGroup.bookReaderTapToPaginate().value.set(true);
    }
  }

  /**
   * Emit fullscreen toggle event
   */
  toggleFullscreen(): void {
    this.updateFullscreen(!this._isFullscreen());
  }


  /**
   * Update parent reading profile preferences
   */
  updateParentProfile(): void {
    const currentRp = this._currentReadingProfile();
    const seriesId = this._currentSeriesId();
    const libraryId = this._currentLibraryId();
    if (!currentRp || currentRp.kind !== ReadingProfileKind.Implicit || !seriesId || !libraryId) {
      return;
    }

    this.readingProfileService.updateParentProfile(libraryId, seriesId, this.packReadingProfile())
      .subscribe(newProfile => {
        this._currentReadingProfile.set(newProfile);
        this.toastr.success(translate('manga-reader.reading-profile-updated'));
      });
  }

  /**
   * Promote implicit profile to named profile
   */
  promoteProfile(): Observable<ReadingProfile> {
    const currentRp = this._currentReadingProfile();
    if (!currentRp || currentRp.kind !== ReadingProfileKind.Implicit) {
      throw new Error('Can only promote implicit profiles');
    }

    return this.readingProfileService.promoteProfile(currentRp.id).pipe(
      tap(newProfile => {
        this._currentReadingProfile.set(newProfile);
      })
    );
  }

  /**
   * Sets up the reactive form and bidirectional binding with signals
   */
  private setFormModelFromReadingProfile(): void {
    const profile = this._currentReadingProfile();
    if (!profile) return;

    this.formModel.set({
      bookReaderDisableBookmarkIcon: profile.bookReaderDisableBookmarkIcon,
      bookReaderFontFamily: profile.bookReaderFontFamily,
      bookReaderFontSize: profile.bookReaderFontSize,
      bookReaderImmersiveMode: profile.bookReaderImmersiveMode,
      bookReaderLayoutMode: profile.bookReaderLayoutMode,
      bookReaderLineSpacing: profile.bookReaderLineSpacing,
      bookReaderMargin: profile.bookReaderMargin,
      bookReaderReadingDirection: profile.bookReaderReadingDirection,
      bookReaderTapToPaginate: profile.bookReaderTapToPaginate,
      bookReaderThemeName: profile.bookReaderThemeName,
      bookReaderWritingStyle: profile.bookReaderWritingStyle,
    });
  }

  /**
   * Resets a selection of settings to their default (Page Styles)
   */
  resetSettings() {
    const defaultStyles = this.getDefaultPageStyles();

    const styles = this.buildPageStyles(
      defaultStyles["font-family"],
      defaultStyles["font-size"],
      defaultStyles['margin-left'],
      defaultStyles['line-height'],
    );

    // Update form to ensure RP is updated, forgive me for the replace...
    this.formGroup.bookReaderFontFamily().value.set(styles['font-family']);
    this.formGroup.bookReaderFontSize().value.set(parseInt(styles["font-size"].replace("%", "")));
    this.formGroup.bookReaderMargin().value.set(parseInt(styles["margin-left"].replace("vw", "")));
    this.formGroup.bookReaderLineSpacing().value.set(parseInt(styles["line-height"].replace("%", "")));
  }


  private updateImplicitProfile(): void {
    if (!this._currentReadingProfile() || !this._currentSeriesId()) return;

    this.readingProfileService.updateImplicit(this._currentLibraryId()!, this._currentSeriesId()!, this.packReadingProfile())
      .subscribe({
        next: newProfile => {
          this._currentReadingProfile.set(newProfile);
        },
        error: err => {
          console.error('Failed to update implicit profile:', err);
        }
      });
  }

  /**
   * Packs current settings into a ReadingProfile object
   */
  private packReadingProfile(): ReadingProfile {
    const currentProfile = this._currentReadingProfile();
    if (!currentProfile) {
      throw new Error('No current reading profile');
    }

    const modelSettings = this.formModel();
    const data = { ...currentProfile };

    // Update from form values
    data.bookReaderFontFamily = modelSettings.bookReaderFontFamily;
    data.bookReaderFontSize = modelSettings.bookReaderFontSize;
    data.bookReaderLineSpacing = modelSettings.bookReaderLineSpacing;
    data.bookReaderMargin = modelSettings.bookReaderMargin;
    data.bookReaderDisableBookmarkIcon = modelSettings.bookReaderDisableBookmarkIcon;

    // Update from signals
    data.bookReaderTapToPaginate = this.clickToPaginate();
    data.bookReaderLayoutMode = this.layoutMode();
    data.bookReaderImmersiveMode = this.immersiveMode();
    data.bookReaderReadingDirection = this.readingDirection();
    data.bookReaderWritingStyle = this.writingStyle();

    const activeTheme = this._activeTheme();
    if (activeTheme) {
      data.bookReaderThemeName = activeTheme.name;
    }

    return data;
  }

  // Maps a stored font family selection to its css font-family value, resolving user fonts to their namespaced alias
  // (preferring a user upload when it shares a built-in family name) so the reader uses the right face on load and change.
  private toCssFontFamily(fontFamily: string | undefined): string {
    if (!fontFamily || fontFamily === FontService.DefaultEpubFont) return 'inherit';
    const matches = this._epubFonts().filter(f => f.family === fontFamily);
    const font = matches.find(f => f.provider === FontProvider.User) ?? matches[0];
    return font ? `'${this.fontService.resolveCssFamily(font)}'` : 'inherit';
  }

  private buildPageStyles(fontFamily?: string, fontSize?: string, margin?: string, lineHeight?: string) {
    const windowWidth = window.innerWidth || this.document.documentElement.clientWidth || this.document.body.clientWidth;
    const mobileBreakpointMarginOverride = 700;

    let defaultMargin = '15vw';
    if (windowWidth <= mobileBreakpointMarginOverride) {
      defaultMargin = '5vw';
    }

    const currentStyles = this.pageStyles();
    const newStyles: PageStyle = {
      'font-family': fontFamily || currentStyles['font-family'] || FontService.DefaultEpubFont,
      'font-size': fontSize || currentStyles['font-size'] || '100%',
      'margin-left': margin || currentStyles['margin-left'] || defaultMargin,
      'margin-right': margin || currentStyles['margin-right'] || defaultMargin,
      'line-height': lineHeight || currentStyles['line-height'] || '100%'
    };

    return newStyles
  }

  public getDefaultPageStyles(): PageStyle {
    return {
      'font-family': FontService.DefaultEpubFont,
      'font-size': '100%',
      'margin-left': '15vw',
      'margin-right': '15vw',
      'line-height': '100%'
    };
  }


  createNewProfileFromImplicit() {
    const rp = this.getCurrentReadingProfile();
    if (rp === null || rp.kind !== ReadingProfileKind.Implicit) {
      return;
    }

    this.promoteProfile().subscribe(newProfile => {
      this._currentReadingProfile.set(newProfile);
      this._parentReadingProfile.set(newProfile);
      this.toastr.success(translate("manga-reader.reading-profile-promoted"));
    });
  }
}
