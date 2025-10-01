import {computed, DestroyRef, effect, inject, Injectable, signal} from '@angular/core';
import {firstValueFrom, Observable, Subject} from 'rxjs';
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
import {ToastrService} from "ngx-toastr";
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {UserBreakpoint, UtilityService} from "../shared/_services/utility.service";
import {environment} from "../../environments/environment";
import {EpubFont} from "../_models/preferences/epub-font";
import {FontService} from "./font.service";

export interface ReaderSettingUpdate {
  setting: 'pageStyle' | 'clickToPaginate' | 'fullscreen' | 'writingStyle' | 'layoutMode' | 'readingDirection' | 'immersiveMode' | 'theme' | 'pageCalcMethod';
  object: any;
}

export type BookReadingProfileFormGroup = FormGroup<{
  bookReaderMargin: FormControl<number>;
  bookReaderLineSpacing: FormControl<number>;
  bookReaderFontSize: FormControl<number>;
  bookReaderFontFamily: FormControl<string>;
  bookReaderTapToPaginate: FormControl<boolean>;
  bookReaderReadingDirection: FormControl<ReadingDirection>;
  bookReaderWritingStyle: FormControl<WritingStyle>;
  bookReaderThemeName: FormControl<string>;
  bookReaderLayoutMode: FormControl<BookPageLayoutMode>;
  bookReaderImmersiveMode: FormControl<boolean>;
}>

@Injectable()
export class EpubReaderSettingsService {
  private readonly destroyRef = inject(DestroyRef);
  private readonly fontService = inject(FontService);
  private readonly themeService = inject(ThemeService);
  private readonly readingProfileService = inject(ReadingProfileService);
  private readonly utilityService = inject(UtilityService);
  private readonly toastr = inject(ToastrService);
  private readonly document = inject(DOCUMENT);
  private readonly fb = inject(NonNullableFormBuilder);

  // Core signals - these will be the single source of truth
  private readonly _currentReadingProfile = signal<ReadingProfile | null>(null);
  private readonly _parentReadingProfile = signal<ReadingProfile | null>(null);
  private readonly _currentSeriesId = signal<number | null>(null);
  private readonly _isInitialized = signal<boolean>(false);
  private readonly _epubFonts = signal<EpubFont[]>([]);

  // Settings signals
  private readonly _pageStyles = signal<PageStyle>(this.getDefaultPageStyles()); // Internal property used to capture all the different css properties to render on all elements
  private readonly _readingDirection = signal<ReadingDirection>(ReadingDirection.LeftToRight);
  private readonly _writingStyle = signal<WritingStyle>(WritingStyle.Horizontal);
  private readonly _activeTheme = signal<BookTheme | undefined>(undefined);
  private readonly _clickToPaginate = signal<boolean>(false);
  private readonly _layoutMode = signal<BookPageLayoutMode>(BookPageLayoutMode.Default);
  private readonly _immersiveMode = signal<boolean>(false);
  private readonly _isFullscreen = signal<boolean>(false);

  // Form will be managed separately but updated from signals
  private settingsForm!: BookReadingProfileFormGroup;
  private isUpdatingFromForm = false; // Flag to prevent infinite loops
  private isInitialized = this._isInitialized(); // Non-signal, updates in effect

  // Event subject for component communication (keep this for now, can be converted to effect later)
  private settingUpdateSubject = new Subject<ReaderSettingUpdate>();

  // Public readonly signals
  public readonly currentReadingProfile = this._currentReadingProfile.asReadonly();
  public readonly parentReadingProfile = this._parentReadingProfile.asReadonly();

  // Settings as readonly signals
  public readonly pageStyles = this._pageStyles.asReadonly();
  public readonly readingDirection = this._readingDirection.asReadonly();
  public readonly writingStyle = this._writingStyle.asReadonly();
  public readonly activeTheme = this._activeTheme.asReadonly();
  public readonly clickToPaginate = this._clickToPaginate.asReadonly();
  public readonly immersiveMode = this._immersiveMode.asReadonly();
  public readonly isFullscreen = this._isFullscreen.asReadonly();
  public readonly epubFonts = this._epubFonts.asReadonly();

  // Computed signals for derived state
  public readonly layoutMode = computed(() => {
    const layout = this._layoutMode();
    const mobileDevice = this.utilityService.activeUserBreakpoint() < UserBreakpoint.Tablet;

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

  // Keep observable for now - can be converted to effect later
  public readonly settingUpdates$ = this.settingUpdateSubject.asObservable()
    .pipe(filter(val => {
    if (!environment.production) {
      console.log(`[SETTINGS EFFECT] ${val.setting}`, val.setting === 'theme' ? val.object.name : val.object);
    }

    return this._isInitialized();
  }), debounceTime(10));

  constructor() {
    // Effect to update form when signals change (only when not updating from form)
    effect(() => {
      const profile = this._currentReadingProfile();
      if (profile && this._isInitialized() && !this.isUpdatingFromForm) {
        this.updateFormFromSignals();
      }
    });


    effect(() => {
      this.isInitialized = this._isInitialized();
    });

    // Effect to emit setting updates when signals change
    effect(() => {
      const styles = this._pageStyles();
      if (!this.isInitialized) return;

      this.settingUpdateSubject.next({
        setting: 'pageStyle',
        object: styles,
      });
    });

    effect(() => {
      const clickToPaginate = this._clickToPaginate();
      if (!this.isInitialized) return;

      this.settingUpdateSubject.next({
        setting: 'clickToPaginate',
        object: clickToPaginate,
      });
    });

    effect(() => {
      const mode = this._layoutMode();
      if (!this.isInitialized) return;

      this.settingUpdateSubject.next({
        setting: 'layoutMode',
        object: mode,
      });
    });

    effect(() => {
      const direction = this._readingDirection();
      if (!this.isInitialized) return;

      this.settingUpdateSubject.next({
        setting: 'readingDirection',
        object: direction,
      });
    });

    effect(() => {
      const style = this._writingStyle();
      if (!this.isInitialized) return;

      this.settingUpdateSubject.next({
        setting: 'writingStyle',
        object: style,
      });
    });

    effect(() => {
      const mode = this._immersiveMode();
      if (!this.isInitialized) return;

      this.settingUpdateSubject.next({
        setting: 'immersiveMode',
        object: mode,
      });
    });

    effect(() => {
      const theme = this._activeTheme();
      if (!this.isInitialized) return;

      if (theme) {
        this.settingUpdateSubject.next({
          setting: 'theme',
          object: theme
        });
      }
    });
  }


  /**
   * Initialize the service with a reading profile and series ID
   */
  async initialize(seriesId: number, readingProfile: ReadingProfile): Promise<void> {
    const fonts = await firstValueFrom(this.fontService.getFonts());
    this._epubFonts.set(fonts);

    this._currentSeriesId.set(seriesId);
    this._currentReadingProfile.set(readingProfile);

    // Load parent profile if needed, otherwise profile is its own parent
    if (readingProfile.kind === ReadingProfileKind.Implicit) {
      try {
        const parent = await firstValueFrom(this.readingProfileService.getForSeries(seriesId, true));
        this._parentReadingProfile.set(parent || null);
      } catch (error) {
        console.error('Failed to load parent reading profile:', error);
      }
    } else {
      this._parentReadingProfile.set(readingProfile);
    }

    // Setup defaults and update signals
    this.setupDefaultsFromProfile(readingProfile);
    this.setupSettingsForm();

    // Set initial theme
    const themeName = readingProfile.bookReaderThemeName || this.themeService.defaultBookTheme;
    this.setTheme(themeName, false);

    // Mark as initialized - this will trigger effects to emit initial values
    this._isInitialized.set(true);
  }

  /**
   * Setup default values and update signals from profile
   */
  private setupDefaultsFromProfile(profile: ReadingProfile): void {
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

    // Update signals from profile
    this._readingDirection.set(profile.bookReaderReadingDirection);
    this._writingStyle.set(profile.bookReaderWritingStyle);
    this._clickToPaginate.set(profile.bookReaderTapToPaginate);
    this._layoutMode.set(profile.bookReaderLayoutMode);
    this._immersiveMode.set(profile.bookReaderImmersiveMode);

    // Set up page styles
    this._pageStyles.set(this.buildPageStyles(
      profile.bookReaderFontFamily,
      profile.bookReaderFontSize + '%',
      profile.bookReaderMargin + 'vw',
      profile.bookReaderLineSpacing + '%'
    ));
  }

  /**
   * Get the current settings form (for components that need direct form access)
   */
  getSettingsForm(): BookReadingProfileFormGroup {
    return this.settingsForm;
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
    const current = this._readingDirection();
    const newDirection = current === ReadingDirection.LeftToRight
      ? ReadingDirection.RightToLeft
      : ReadingDirection.LeftToRight;

    this._readingDirection.set(newDirection);
    this.settingsForm.get('bookReaderReadingDirection')!.setValue(newDirection);
  }

  /**
   * Toggle writing style
   */
  toggleWritingStyle(): void {
    const current = this._writingStyle();
    const newStyle = current === WritingStyle.Horizontal
      ? WritingStyle.Vertical
      : WritingStyle.Horizontal;

    // Default back to Col 1 in this case
    if (newStyle === WritingStyle.Vertical ) {
      if (this._layoutMode() === BookPageLayoutMode.Column2) {
        this.updateLayoutMode(BookPageLayoutMode.Column1);
      }
    }

    this._writingStyle.set(newStyle);
    this.settingsForm.get('bookReaderWritingStyle')!.setValue(newStyle);
  }

  /**
   * Set theme
   */
  setTheme(themeName: string, update: boolean = true): void {
    const theme = bookColorThemes.find(t => t.name === themeName);
    if (theme) {
      this._activeTheme.set(theme);
      if (update) {
        this.settingsForm.get('bookReaderThemeName')!.setValue(themeName);
      }
    }
  }

  updateLayoutMode(mode: BookPageLayoutMode): void {
    this._layoutMode.set(mode);
    // Update form control to keep in sync
    this.settingsForm.get('bookReaderLayoutMode')?.setValue(mode, { emitEvent: false });
  }

  updateClickToPaginate(value: boolean): void {
    this._clickToPaginate.set(value);
    this.settingsForm.get('bookReaderTapToPaginate')?.setValue(value);
  }

  updateReadingDirection(value: ReadingDirection): void {
    this._readingDirection.set(value);
    this.settingsForm.get('bookReaderReadingDirection')?.setValue(value);
  }

  updateWritingStyle(value: WritingStyle) {
    this._writingStyle.set(value);
    this.settingsForm.get('bookReaderWritingStyle')?.setValue(value);
  }

  updateFullscreen(value: boolean) {
    this._isFullscreen.set(value);
    if (!this._isInitialized()) return;

    this.settingUpdateSubject.next({ setting: 'fullscreen', object: null }); // TODO: Refactor into an effect
  }

  updateImmersiveMode(value: boolean): void {
    this._immersiveMode.set(value);
    if (value) {
      this._clickToPaginate.set(true);
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
    if (!currentRp || currentRp.kind !== ReadingProfileKind.Implicit || !seriesId) {
      return;
    }

    this.readingProfileService.updateParentProfile(seriesId, this.packReadingProfile())
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
   * Update form controls from current signal values
   */
  private updateFormFromSignals(): void {
    const profile = this._currentReadingProfile();
    if (!profile) return;

    // Update form controls without triggering valueChanges
    this.settingsForm.patchValue({
      bookReaderFontFamily: profile.bookReaderFontFamily,
      bookReaderFontSize: profile.bookReaderFontSize,
      bookReaderTapToPaginate: this._clickToPaginate(),
      bookReaderLineSpacing: profile.bookReaderLineSpacing,
      bookReaderMargin: profile.bookReaderMargin,
      bookReaderLayoutMode: this._layoutMode(),
      bookReaderImmersiveMode: this._immersiveMode()
    }, { emitEvent: false });
  }

  /**
   * Sets up the reactive form and bidirectional binding with signals
   */
  private setupSettingsForm(): void {
    const profile = this._currentReadingProfile();
    if (!profile) return;

    // Recreate the form
    this.settingsForm = new FormGroup({
      bookReaderMargin: this.fb.control(profile.bookReaderMargin),
      bookReaderLineSpacing: this.fb.control(profile.bookReaderLineSpacing),
      bookReaderFontSize: this.fb.control(profile.bookReaderFontSize),
      bookReaderFontFamily: this.fb.control(profile.bookReaderFontFamily),
      bookReaderTapToPaginate: this.fb.control(this._clickToPaginate()),
      bookReaderReadingDirection: this.fb.control(this._readingDirection()),
      bookReaderWritingStyle: this.fb.control(profile.bookReaderWritingStyle),
      bookReaderThemeName: this.fb.control(profile.bookReaderThemeName),
      bookReaderLayoutMode: this.fb.control(this._layoutMode()),
      bookReaderImmersiveMode: this.fb.control(this._immersiveMode()),
    });

    // Set up value change subscriptions
    this.setupFormSubscriptions();
  }

  /**
   * Sets up form value change subscriptions to update signals
   */
  private setupFormSubscriptions(): void {
    // Font family changes
    this.settingsForm.get('bookReaderFontFamily')?.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(fontName => {
      this.isUpdatingFromForm = true;

      const familyName = this._epubFonts().find(f => f.name === fontName)?.name || FontService.DefaultEpubFont;
      const currentStyles = this._pageStyles();

      const newStyles = { ...currentStyles };
      if (familyName === FontService.DefaultEpubFont) {
        newStyles['font-family'] = 'inherit';
      } else {
        newStyles['font-family'] = `'${familyName}'`;
      }

      this._pageStyles.set(newStyles);
      this.isUpdatingFromForm = false;
    });

    // Font size changes
    this.settingsForm.get('bookReaderFontSize')?.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(value => {
      this.isUpdatingFromForm = true;

      const currentStyles = this._pageStyles();
      const newStyles = { ...currentStyles };
      newStyles['font-size'] = value + '%';
      this._pageStyles.set(newStyles);

      this.isUpdatingFromForm = false;
    });

    // Tap to paginate changes
    this.settingsForm.get('bookReaderTapToPaginate')?.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(value => {
      this.isUpdatingFromForm = true;
      this._clickToPaginate.set(value);
      this.isUpdatingFromForm = false;
    });

    // Line spacing changes
    this.settingsForm.get('bookReaderLineSpacing')?.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(value => {
      this.isUpdatingFromForm = true;

      const currentStyles = this._pageStyles();
      const newStyles = { ...currentStyles };
      newStyles['line-height'] = value + '%';
      this._pageStyles.set(newStyles);

      this.isUpdatingFromForm = false;
    });

    // Margin changes
    this.settingsForm.get('bookReaderMargin')?.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(value => {
      this.isUpdatingFromForm = true;

      const currentStyles = this._pageStyles();
      const newStyles = { ...currentStyles };
      newStyles['margin-left'] = value + 'vw';
      newStyles['margin-right'] = value + 'vw';
      this._pageStyles.set(newStyles);

      this.isUpdatingFromForm = false;
    });

    // Layout mode changes
    this.settingsForm.get('bookReaderLayoutMode')?.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((layoutMode: BookPageLayoutMode) => {
      this.isUpdatingFromForm = true;

      if (this.writingStyle() === WritingStyle.Vertical && layoutMode === BookPageLayoutMode.Column2) {
        this.toastr.info(translate('book-reader.forced-vertical-switch'));
        this.isUpdatingFromForm = false;
        return;
      }


      this._layoutMode.set(layoutMode);
      this.isUpdatingFromForm = false;
    });

    // Immersive mode changes
    this.settingsForm.get('bookReaderImmersiveMode')?.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((immersiveMode: boolean) => {
      this.isUpdatingFromForm = true;

      if (immersiveMode) {
        this.settingsForm.get('bookReaderTapToPaginate')?.setValue(true, { emitEvent: false });
        this._clickToPaginate.set(true);
      }
      this._immersiveMode.set(immersiveMode);

      this.isUpdatingFromForm = false;
    });


    // Update implicit profile on form changes (debounced) - ONLY source of profile updates
    this.settingsForm.valueChanges.pipe(
      debounceTime(500),
      distinctUntilChanged(),
      takeUntilDestroyed(this.destroyRef),
      filter(() => !this.isUpdatingFromForm),
      tap(() => this.updateImplicitProfile()),
    ).subscribe();
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
    this.settingsForm.get('bookReaderFontFamily')!.setValue(styles["font-family"]);
    this.settingsForm.get('bookReaderFontSize')!.setValue(parseInt(styles["font-size"].replace("%", "")));
    this.settingsForm.get('bookReaderMargin')!.setValue(parseInt(styles["margin-left"].replace("vw", "")));
    this.settingsForm.get('bookReaderLineSpacing')!.setValue(parseInt(styles["line-height"].replace("%", "")));
  }


  private updateImplicitProfile(): void {
    if (!this._currentReadingProfile() || !this._currentSeriesId()) return;

    this.readingProfileService.updateImplicit(this.packReadingProfile(), this._currentSeriesId()!)
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

    const modelSettings = this.settingsForm.getRawValue();
    const data = { ...currentProfile };

    // Update from form values
    data.bookReaderFontFamily = modelSettings.bookReaderFontFamily;
    data.bookReaderFontSize = modelSettings.bookReaderFontSize;
    data.bookReaderLineSpacing = modelSettings.bookReaderLineSpacing;
    data.bookReaderMargin = modelSettings.bookReaderMargin;

    // Update from signals
    data.bookReaderTapToPaginate = this._clickToPaginate();
    data.bookReaderLayoutMode = this._layoutMode();
    data.bookReaderImmersiveMode = this._immersiveMode();
    data.bookReaderReadingDirection = this._readingDirection();
    data.bookReaderWritingStyle = this._writingStyle();

    const activeTheme = this._activeTheme();
    if (activeTheme) {
      data.bookReaderThemeName = activeTheme.name;
    }

    return data;
  }

  private buildPageStyles(fontFamily?: string, fontSize?: string, margin?: string, lineHeight?: string) {
    const windowWidth = window.innerWidth || this.document.documentElement.clientWidth || this.document.body.clientWidth;
    const mobileBreakpointMarginOverride = 700;

    let defaultMargin = '15vw';
    if (windowWidth <= mobileBreakpointMarginOverride) {
      defaultMargin = '5vw';
    }

    const currentStyles = this._pageStyles();
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
