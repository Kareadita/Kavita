import {NgClass, NgStyle, NgTemplateOutlet, TitleCasePipe} from '@angular/common';
import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, Input, OnInit} from '@angular/core';
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
import {
  NgbAccordionBody,
  NgbAccordionButton,
  NgbAccordionCollapse,
  NgbAccordionDirective,
  NgbAccordionHeader,
  NgbAccordionItem,
  NgbTooltip
} from '@ng-bootstrap/ng-bootstrap';
import {TranslocoDirective} from "@jsverse/transloco";
import {ReadingProfileService} from "../../../_services/reading-profile.service";
import {ReadingProfile, ReadingProfileKind} from "../../../_models/preferences/reading-profiles";
import {EpubReaderSettingsService} from "../../../_services/epub-reader-settings.service";

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

  @Input({required:true}) seriesId!: number;
  @Input({required:true}) readingProfile!: ReadingProfile;

  /**
   * List of all font families user can select from
   */
  fontOptions: Array<string> = [];
  fontFamilies: Array<FontFamily> = [];
  settingsForm: FormGroup = new FormGroup({});
  /**
   * System provided themes
   */
  themes: Array<BookTheme> = this.readerSettingsService.getThemes();

  protected readonly pageStyles = this.readerSettingsService.pageStyles;
  protected readonly readingDirectionModel = this.readerSettingsService.readingDirection;
  protected readonly writingStyleModel = this.readerSettingsService.writingStyle;
  protected readonly activeTheme = this.readerSettingsService.activeTheme;
  protected readonly layoutMode = this.readerSettingsService.layoutMode;
  protected readonly immersiveMode = this.readerSettingsService.immersiveMode;
  protected readonly clickToPaginate = this.readerSettingsService.clickToPaginate;
  protected readonly isFullscreen = this.readerSettingsService.isFullscreen;
  protected readonly canPromoteProfile = this.readerSettingsService.canPromoteProfile;
  protected readonly hasParentProfile = this.readerSettingsService.hasParentProfile;
  protected readonly parentReadingProfile = this.readerSettingsService.parentReadingProfile;
  protected readonly currentReadingProfile = this.readerSettingsService.currentReadingProfile;


  async ngOnInit() {
    // Initialize the service if not already done
    if (!this.readerSettingsService.getCurrentReadingProfile()) {
      await this.readerSettingsService.initialize(this.seriesId, this.readingProfile);
    }

    this.settingsForm = this.readerSettingsService.getSettingsForm();
    this.fontFamilies = this.readerSettingsService.getFontFamilies();
    this.fontOptions = this.fontFamilies.map(f => f.title);
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
    this.readerSettingsService.toggleFullscreen();
    this.cdRef.markForCheck();
  }

  // menu only code
  updateParentPref() {
    this.readerSettingsService.updateParentProfile();
  }

  createNewProfileFromImplicit() {
    this.readerSettingsService.createNewProfileFromImplicit();
  }


  protected readonly ReadingProfileKind = ReadingProfileKind;
  protected readonly WritingStyle = WritingStyle;
  protected readonly ReadingDirection = ReadingDirection;
  protected readonly BookPageLayoutMode = BookPageLayoutMode;
}
