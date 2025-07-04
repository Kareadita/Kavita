import {NgClass, NgStyle, NgTemplateOutlet, TitleCasePipe} from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  EventEmitter,
  inject,
  Input,
  OnInit,
  Output
} from '@angular/core';
import {FormGroup, ReactiveFormsModule} from '@angular/forms';
import {BookPageLayoutMode} from 'src/app/_models/readers/book-page-layout-mode';
import {BookTheme} from 'src/app/_models/preferences/book-theme';
import {ReadingDirection} from 'src/app/_models/preferences/reading-direction';
import {WritingStyle} from 'src/app/_models/preferences/writing-style';
import {ThemeProvider} from 'src/app/_models/preferences/site-theme';
import {FontFamily} from '../../_services/book.service';
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
import {ToastrService} from "ngx-toastr";
import {EpubReaderSettingsService} from "../../../_services/epub-reader-settings.service";
import {tap} from "rxjs/operators";

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

@Component({
    selector: 'app-reader-settings',
    templateUrl: './reader-settings.component.html',
    styleUrls: ['./reader-settings.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, NgbAccordionDirective, NgbAccordionItem, NgbAccordionHeader, NgbAccordionButton,
      NgbAccordionCollapse, NgbAccordionBody, NgbTooltip, NgTemplateOutlet, NgClass, NgStyle,
      TitleCasePipe, TranslocoDirective]
})
export class ReaderSettingsComponent implements OnInit {

  private readonly readerSettingsService = inject(EpubReaderSettingsService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly readingProfileService = inject(ReadingProfileService);
  private readonly toastr = inject(ToastrService);

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

  /**
   * List of all font families user can select from
   */
  fontOptions: Array<string> = [];
  fontFamilies: Array<FontFamily> = [];
  /**
   * Internal property used to capture all the different css properties to render on all elements
   */
  pageStyles: PageStyle = this.readerSettingsService.getDefaultPageStyles();

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



  async ngOnInit() {

    // Initialize the service if not already done
    if (!this.readerSettingsService.getCurrentReadingProfile()) {
      await this.readerSettingsService.initialize(this.seriesId, this.readingProfile);
    }

    this.readerSettingsService.readingProfile$.pipe(
      takeUntilDestroyed(this.destroyRef),
      tap((profile) => {
        if (profile) {
          this.readingProfile = profile;
          this.cdRef.markForCheck();
        }
      })
    ).subscribe();

    this.settingsForm = this.readerSettingsService.getSettingsForm();
    this.fontFamilies = this.readerSettingsService.getFontFamilies();
    this.fontOptions = this.fontFamilies.map(f => f.title);
    this.themes = this.readerSettingsService.getThemes();


    // Subscribe to service state
    this.readerSettingsService.pageStyles$.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(styles => {
      this.pageStyles = styles;
      this.cdRef.markForCheck();
    });

    this.readerSettingsService.readingDirection$.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(direction => {
      this.readingDirectionModel = direction;
      this.cdRef.markForCheck();
    });

    this.readerSettingsService.writingStyle$.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(style => {
      this.writingStyleModel = style;
      this.cdRef.markForCheck();
    });

    this.readerSettingsService.activeTheme$.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(theme => {
      this.activeTheme = theme;
      this.cdRef.markForCheck();
    });

    // Handle parent reading profile
    if (this.readingProfile.kind === ReadingProfileKind.Implicit) {
      this.readingProfileService.getForSeries(this.seriesId, true).subscribe(parent => {
        this.parentReadingProfile = parent;
        this.cdRef.markForCheck();
      });
    } else {
      this.parentReadingProfile = this.readingProfile;
    }

    this.cdRef.markForCheck();
  }

  resetSettings() {
    this.readerSettingsService.resetSettings();
  }

  setTheme(themeName: string, update: boolean = true) {
    this.readerSettingsService.setTheme(themeName, update);
  }

  toggleReadingDirection() {
    this.readerSettingsService.toggleReadingDirection();
  }

  toggleWritingStyle() {
    this.readerSettingsService.toggleWritingStyle();
  }

  toggleFullscreen() {
    this.isFullscreen = !this.isFullscreen;
    this.readerSettingsService.toggleFullscreen();
    this.cdRef.markForCheck();
  }

  // menu only code
  updateParentPref() {
    this.readerSettingsService.updateParentProfile();
    this.toastr.success(translate('manga-reader.reading-profile-updated'));
  }

  createNewProfileFromImplicit() {
    if (this.readingProfile.kind !== ReadingProfileKind.Implicit) {
      return;
    }

    this.readerSettingsService.promoteProfile().subscribe(newProfile => {
      this.readingProfile = newProfile;
      this.parentReadingProfile = newProfile;
      this.cdRef.markForCheck();
      this.toastr.success(translate("manga-reader.reading-profile-promoted"));
    });
  }


  protected readonly ReadingProfileKind = ReadingProfileKind;
  protected readonly WritingStyle = WritingStyle;
  protected readonly ReadingDirection = ReadingDirection;
  protected readonly BookPageLayoutMode = BookPageLayoutMode;
}
