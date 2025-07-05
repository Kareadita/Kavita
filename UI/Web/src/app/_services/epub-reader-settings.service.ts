import {DestroyRef, inject, Injectable} from '@angular/core';
import {BehaviorSubject, distinctUntilChanged, Observable, Subject} from 'rxjs';
import {bookColorThemes, PageStyle} from "../book-reader/_components/reader-settings/reader-settings.component";
import {ReadingDirection} from '../_models/preferences/reading-direction';
import {WritingStyle} from '../_models/preferences/writing-style';
import {BookPageLayoutMode} from "../_models/readers/book-page-layout-mode";
import {FormControl, FormGroup} from "@angular/forms";
import {ReadingProfile, ReadingProfileKind} from "../_models/preferences/reading-profiles";
import {BookService, FontFamily} from "../book-reader/_services/book.service";
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {ThemeService} from './theme.service';
import {ReadingProfileService} from "./reading-profile.service";
import {debounceTime, skip, tap} from "rxjs/operators";
import {BookTheme} from "../_models/preferences/book-theme";
import {DOCUMENT} from "@angular/common";
import {translate} from "@jsverse/transloco";
import {ToastrService} from "ngx-toastr";

export interface ReaderSettingUpdate {
  setting: 'pageStyle' | 'clickToPaginate' | 'fullscreen' | 'writingStyle' | 'layoutMode' | 'readingDirection' | 'immersiveMode' | 'theme';
  object: any;
}


@Injectable({
  providedIn: 'root'
})
export class EpubReaderSettingsService {
  private readonly destroyRef = inject(DestroyRef);
  private readonly bookService = inject(BookService);
  private readonly themeService = inject(ThemeService);
  private readonly readingProfileService = inject(ReadingProfileService);
  private readonly toastr = inject(ToastrService);
  private readonly document = inject(DOCUMENT);

  private pageStylesSubject = new BehaviorSubject<PageStyle>(this.getDefaultPageStyles());
  private readingDirectionSubject = new BehaviorSubject<ReadingDirection>(ReadingDirection.LeftToRight);
  private writingStyleSubject = new BehaviorSubject<WritingStyle>(WritingStyle.Horizontal);
  // @ts-ignore
  private activeThemeSubject = new BehaviorSubject<BookTheme | undefined>(undefined);
  private clickToPaginateSubject = new BehaviorSubject<boolean>(false);
  private layoutModeSubject = new BehaviorSubject<BookPageLayoutMode>(BookPageLayoutMode.Default);
  private immersiveModeSubject = new BehaviorSubject<boolean>(false);
  private readingProfileSubject = new BehaviorSubject<ReadingProfile | null>(null);

  // Event subjects for component communication
  private settingUpdateSubject = new Subject<ReaderSettingUpdate>();

  // Form and data
  private settingsForm: FormGroup = new FormGroup({});
  private currentReadingProfile: ReadingProfile | null = null;
  private parentReadingProfile: ReadingProfile | null = null;
  private currentSeriesId: number | null = null;
  private fontFamilies: FontFamily[] = this.bookService.getFontFamilies();

  // Public observables
  public readonly pageStyles$ = this.pageStylesSubject.asObservable();
  public readonly readingDirection$ = this.readingDirectionSubject.asObservable();
  public readonly writingStyle$ = this.writingStyleSubject.asObservable();
  public readonly activeTheme$ = this.activeThemeSubject.asObservable();
  public readonly clickToPaginate$ = this.clickToPaginateSubject.asObservable();
  public readonly layoutMode$ = this.layoutModeSubject.asObservable();
  public readonly immersiveMode$ = this.immersiveModeSubject.asObservable();
  public readonly readingProfile$ = this.readingProfileSubject.asObservable();
  public readonly settingUpdates$ = this.settingUpdateSubject.asObservable();



  /**
   * Initialize the service with a reading profile and series ID
   * This should be called when the reader starts up
   */
  async initialize(seriesId: number, readingProfile: ReadingProfile): Promise<void> {
    this.currentSeriesId = seriesId;
    this.currentReadingProfile = readingProfile;
    console.log('init, reading profile: ', readingProfile);
    this.readingProfileSubject.next(readingProfile);

    // Load parent profile if needed
    if (readingProfile.kind === ReadingProfileKind.Implicit) {
      try {
       const parent = await this.readingProfileService.getForSeries(seriesId, true).toPromise();
       this.parentReadingProfile = parent || null;
        // Keep the implicit profile but use parent as reference (TODO: Validate the code)
      } catch (error) {
        console.error('Failed to load parent reading profile:', error);
      }
    }

    // Setup defaults and form
    this.setupDefaultSettings();


    // Set initial theme
    const themeName = readingProfile.bookReaderThemeName || this.themeService.defaultBookTheme;
    this.setTheme(themeName, false);

    // Emit initial values
    this.emitInitialSettings();
  }

  /**
   * Get the current settings form (for components that need direct form access)
   */
  getSettingsForm(): FormGroup {
    return this.settingsForm;
  }

  /**
   * Get current reading profile
   */
  getCurrentReadingProfile(): ReadingProfile | null {
    return this.currentReadingProfile;
  }

  /**
   * Get font families for UI
   */
  getFontFamilies(): FontFamily[] {
    return this.fontFamilies;
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
    const current = this.readingDirectionSubject.value;
    const newDirection = current === ReadingDirection.LeftToRight
      ? ReadingDirection.RightToLeft
      : ReadingDirection.LeftToRight;

    this.readingDirectionSubject.next(newDirection);
    this.settingUpdateSubject.next({ setting: 'readingDirection', object: newDirection });
    this.updateImplicitProfile();
  }

  /**
   * Toggle writing style
   */
  toggleWritingStyle(): void {
    const current = this.writingStyleSubject.value;
    const newStyle = current === WritingStyle.Horizontal
      ? WritingStyle.Vertical
      : WritingStyle.Horizontal;

    this.writingStyleSubject.next(newStyle);
    this.settingUpdateSubject.next({ setting: 'writingStyle', object: newStyle });
    this.updateImplicitProfile();
  }

  /**
   * Set theme
   */
  setTheme(themeName: string, update: boolean = true): void {
    const theme = bookColorThemes.find(t => t.name === themeName);
    if (theme) {
      this.activeThemeSubject.next(theme);
      this.settingUpdateSubject.next({ setting: 'theme', object: theme });

      if (update) {
        this.updateImplicitProfile();
      }
    }
  }

  /**
   * Emit fullscreen toggle event
   */
  toggleFullscreen(): void {
    this.settingUpdateSubject.next({ setting: 'fullscreen', object: null });
  }

  /**
   * Update parent reading profile preferences
   */
  updateParentProfile(): void {
    if (!this.currentReadingProfile || this.currentReadingProfile.kind !== ReadingProfileKind.Implicit || !this.currentSeriesId) {
      return;
    }

    this.readingProfileService.updateParentProfile(this.currentSeriesId, this.packReadingProfile())
      .subscribe(newProfile => {
        this.currentReadingProfile = newProfile;
        this.readingProfileSubject.next(newProfile);
      });
  }

  /**
   * Promote implicit profile to named profile
   */
  promoteProfile(): Observable<ReadingProfile> {
    if (!this.currentReadingProfile || this.currentReadingProfile.kind !== ReadingProfileKind.Implicit) {
      throw new Error('Can only promote implicit profiles');
    }

    return this.readingProfileService.promoteProfile(this.currentReadingProfile.id).pipe(
      tap(newProfile => {
        this.currentReadingProfile = newProfile;
        this.readingProfileSubject.next(newProfile);
      })
    );
  }


  private setupDefaultSettings(): void {
    if (!this.currentReadingProfile) return;

    // Set up defaults
    const profile = this.currentReadingProfile;
    if (profile.bookReaderFontFamily === undefined) {
      profile.bookReaderFontFamily = 'default';
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

    this.setupSettingsForm();

    // Update internal state
    this.readingDirectionSubject.next(profile.bookReaderReadingDirection);
    this.writingStyleSubject.next(profile.bookReaderWritingStyle);
    this.clickToPaginateSubject.next(profile.bookReaderTapToPaginate);
    this.layoutModeSubject.next(profile.bookReaderLayoutMode);
    this.immersiveModeSubject.next(profile.bookReaderImmersiveMode);

    // Set up page styles
    this.setPageStyles(
      profile.bookReaderFontFamily,
      profile.bookReaderFontSize + '%',
      profile.bookReaderMargin + 'vw',
      profile.bookReaderLineSpacing + '%'
    );
  }

  private setupSettingsForm(): void {
    if (!this.currentReadingProfile) return;

    const profile = this.currentReadingProfile;

    // Clear existing form
    this.settingsForm = new FormGroup({});

    // Add controls
    this.settingsForm.addControl('bookReaderFontFamily', new FormControl(profile.bookReaderFontFamily));
    this.settingsForm.addControl('bookReaderFontSize', new FormControl(profile.bookReaderFontSize));
    this.settingsForm.addControl('bookReaderTapToPaginate', new FormControl(profile.bookReaderTapToPaginate));
    this.settingsForm.addControl('bookReaderLineSpacing', new FormControl(profile.bookReaderLineSpacing));
    this.settingsForm.addControl('bookReaderMargin', new FormControl(profile.bookReaderMargin));
    this.settingsForm.addControl('layoutMode', new FormControl(profile.bookReaderLayoutMode));
    this.settingsForm.addControl('bookReaderImmersiveMode', new FormControl(profile.bookReaderImmersiveMode));

    // Set up value change subscriptions
    this.setupFormSubscriptions();
  }

  private setupFormSubscriptions(): void {
    // Font family changes
    this.settingsForm.get('bookReaderFontFamily')?.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(fontName => {
      const familyName = this.fontFamilies.find(f => f.title === fontName)?.family || 'default';
      const currentStyles = this.pageStylesSubject.value;

      if (familyName === 'default') {
        currentStyles['font-family'] = 'inherit';
      } else {
        currentStyles['font-family'] = `'${familyName}'`;
      }

      this.pageStylesSubject.next({ ...currentStyles });
      this.settingUpdateSubject.next({ setting: 'pageStyle', object: currentStyles });
    });

    // Font size changes
    this.settingsForm.get('bookReaderFontSize')?.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(value => {
      const currentStyles = this.pageStylesSubject.value;
      currentStyles['font-size'] = value + '%';
      this.pageStylesSubject.next({ ...currentStyles });
      this.settingUpdateSubject.next({ setting: 'pageStyle', object: currentStyles });
    });

    // Tap to paginate changes
    this.settingsForm.get('bookReaderTapToPaginate')?.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(value => {
      this.clickToPaginateSubject.next(value);
      this.settingUpdateSubject.next({ setting: 'clickToPaginate', object: value });
    });

    // Line spacing changes
    this.settingsForm.get('bookReaderLineSpacing')?.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(value => {
      const currentStyles = this.pageStylesSubject.value;
      currentStyles['line-height'] = value + '%';
      this.pageStylesSubject.next({ ...currentStyles });
      this.settingUpdateSubject.next({ setting: 'pageStyle', object: currentStyles });
    });

    // Margin changes
    this.settingsForm.get('bookReaderMargin')?.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(value => {
      const currentStyles = this.pageStylesSubject.value;
      currentStyles['margin-left'] = value + 'vw';
      currentStyles['margin-right'] = value + 'vw';
      this.pageStylesSubject.next({ ...currentStyles });
      this.settingUpdateSubject.next({ setting: 'pageStyle', object: currentStyles });
    });

    // Layout mode changes
    this.settingsForm.get('layoutMode')?.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((layoutMode: BookPageLayoutMode) => {
      this.layoutModeSubject.next(layoutMode);
      this.settingUpdateSubject.next({ setting: 'layoutMode', object: layoutMode });
    });

    // Immersive mode changes
    this.settingsForm.get('bookReaderImmersiveMode')?.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((immersiveMode: boolean) => {
      if (immersiveMode) {
        this.settingsForm.get('bookReaderTapToPaginate')?.setValue(true);
      }
      this.immersiveModeSubject.next(immersiveMode);
      this.settingUpdateSubject.next({ setting: 'immersiveMode', object: immersiveMode });
    });

    // Update implicit profile on form changes
    this.settingsForm.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      skip(1), // Skip initial form creation
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => {
      this.updateImplicitProfile();
    });
  }

  /**
   * Resets a selection of settings to their default (Page Styles)
   */
  resetSettings() {
    const defaultStyles = this.getDefaultPageStyles();
    this.setPageStyles(
      defaultStyles["font-family"],
      defaultStyles["font-size"],
      defaultStyles['margin-left'] ,
      defaultStyles['line-height'],
    );
  }

  private emitInitialSettings(): void {
    // Emit all current settings so the reader can initialize properly
    this.settingUpdateSubject.next({ setting: 'pageStyle', object: this.pageStylesSubject.value });
    this.settingUpdateSubject.next({ setting: 'clickToPaginate', object: this.clickToPaginateSubject.value });
    this.settingUpdateSubject.next({ setting: 'layoutMode', object: this.layoutModeSubject.value });
    this.settingUpdateSubject.next({ setting: 'readingDirection', object: this.readingDirectionSubject.value });
    this.settingUpdateSubject.next({ setting: 'writingStyle', object: this.writingStyleSubject.value });
    this.settingUpdateSubject.next({ setting: 'immersiveMode', object: this.immersiveModeSubject.value });

    const activeTheme = this.activeThemeSubject.value;
    if (activeTheme) {
      this.settingUpdateSubject.next({ setting: 'theme', object: activeTheme });
    }
  }

  private updateImplicitProfile(): void {
    if (!this.currentReadingProfile || !this.currentSeriesId) return;

    this.readingProfileService.updateImplicit(this.packReadingProfile(), this.currentSeriesId)
      .subscribe({
        next: newProfile => {
          this.currentReadingProfile = newProfile;
          this.readingProfileSubject.next(newProfile);
        },
        error: err => {
          console.error('Failed to update implicit profile:', err);
        }
      });
  }

  private packReadingProfile(): ReadingProfile {
    if (!this.currentReadingProfile) {
      throw new Error('No current reading profile');
    }

    const modelSettings = this.settingsForm.getRawValue();
    const data = { ...this.currentReadingProfile };

    data.bookReaderFontFamily = modelSettings.bookReaderFontFamily;
    data.bookReaderFontSize = modelSettings.bookReaderFontSize;
    data.bookReaderLineSpacing = modelSettings.bookReaderLineSpacing;
    data.bookReaderMargin = modelSettings.bookReaderMargin;
    data.bookReaderTapToPaginate = modelSettings.bookReaderTapToPaginate;
    data.bookReaderLayoutMode = modelSettings.layoutMode;
    data.bookReaderImmersiveMode = modelSettings.bookReaderImmersiveMode;

    data.bookReaderReadingDirection = this.readingDirectionSubject.value;
    data.bookReaderWritingStyle = this.writingStyleSubject.value;

    const activeTheme = this.activeThemeSubject.value;
    if (activeTheme) {
      data.bookReaderThemeName = activeTheme.name;
    }

    console.log('packed reading profile:', data);

    return data;
  }

  private setPageStyles(fontFamily?: string, fontSize?: string, margin?: string, lineHeight?: string): void {
    const windowWidth = window.innerWidth || this.document.documentElement.clientWidth || this.document.body.clientWidth;
    const mobileBreakpointMarginOverride = 700;

    let defaultMargin = '15vw';
    if (windowWidth <= mobileBreakpointMarginOverride) {
      defaultMargin = '5vw';
    }

    const currentStyles = this.pageStylesSubject.value;
    const newStyles: PageStyle = {
      'font-family': fontFamily || currentStyles['font-family'] || 'default',
      'font-size': fontSize || currentStyles['font-size'] || '100%',
      'margin-left': margin || currentStyles['margin-left'] || defaultMargin,
      'margin-right': margin || currentStyles['margin-right'] || defaultMargin,
      'line-height': lineHeight || currentStyles['line-height'] || '100%'
    };

    this.pageStylesSubject.next(newStyles);
    this.updateImplicitProfile();
    this.settingUpdateSubject.next({ setting: 'pageStyle', object: newStyles });
  }

  public getDefaultPageStyles(): PageStyle {
    return {
      'font-family': 'default',
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
      this.currentReadingProfile = newProfile;
      this.parentReadingProfile = newProfile;
      this.toastr.success(translate("manga-reader.reading-profile-promoted"));
    });
  }
}
