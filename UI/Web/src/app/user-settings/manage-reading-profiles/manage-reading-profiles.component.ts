import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, OnInit} from '@angular/core';
import {ReadingProfileService} from "../../_services/reading-profile.service";
import {
  bookLayoutModes, bookWritingStyles, layoutModes,
  pageSplitOptions, pdfScrollModes,
  pdfSpreadModes, pdfThemes,
  readingDirections, readingModes,
  ReadingProfile, scalingOptions
} from "../../_models/preferences/reading-profiles";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {Location, NgStyle, NgTemplateOutlet, TitleCasePipe} from "@angular/common";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";
import {User} from "../../_models/user";
import {AccountService} from "../../_services/account.service";
import {debounceTime, distinctUntilChanged, take, tap} from "rxjs/operators";
import {SentenceCasePipe} from "../../_pipes/sentence-case.pipe";
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators} from "@angular/forms";
import {BookService} from "../../book-reader/_services/book.service";
import {BookPageLayoutMode} from "../../_models/readers/book-page-layout-mode";
import {PdfTheme} from "../../_models/preferences/pdf-theme";
import {PdfScrollMode} from "../../_models/preferences/pdf-scroll-mode";
import {PdfSpreadMode} from "../../_models/preferences/pdf-spread-mode";
import {bookColorThemes} from "../../book-reader/_components/reader-settings/reader-settings.component";
import {BookPageLayoutModePipe} from "../../_pipes/book-page-layout-mode.pipe";
import {LayoutModePipe} from "../../_pipes/layout-mode.pipe";
import {PageSplitOptionPipe} from "../../_pipes/page-split-option.pipe";
import {PdfScrollModePipe} from "../../_pipes/pdf-scroll-mode.pipe";
import {PdfSpreadModePipe} from "../../_pipes/pdf-spread-mode.pipe";
import {PdfThemePipe} from "../../_pipes/pdf-theme.pipe";
import {ReaderModePipe} from "../../_pipes/reading-mode.pipe";
import {ReadingDirectionPipe} from "../../_pipes/reading-direction.pipe";
import {ScalingOptionPipe} from "../../_pipes/scaling-option.pipe";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {SettingSwitchComponent} from "../../settings/_components/setting-switch/setting-switch.component";
import {WritingStylePipe} from "../../_pipes/writing-style.pipe";
import {ColorPickerDirective} from "ngx-color-picker";
import {
  NgbNav,
  NgbNavItem,
  NgbNavLinkBase,
  NgbNavContent,
  NgbNavOutlet
} from "@ng-bootstrap/ng-bootstrap";
import {filter} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {LoadingComponent} from "../../shared/loading/loading.component";

enum TabId {
  ImageReader = "image-reader",
  BookReader = "book-reader",
  PdfReader = "pdf-reader",
  Series = "series",
  Libraries = "libraries",
}

@Component({
  selector: 'app-manage-reading-profiles',
  imports: [
    TranslocoDirective,
    NgTemplateOutlet,
    VirtualScrollerModule,
    SentenceCasePipe,
    BookPageLayoutModePipe,
    FormsModule,
    LayoutModePipe,
    PageSplitOptionPipe,
    PdfScrollModePipe,
    PdfSpreadModePipe,
    PdfThemePipe,
    ReactiveFormsModule,
    ReaderModePipe,
    ReadingDirectionPipe,
    ScalingOptionPipe,
    SettingItemComponent,
    SettingSwitchComponent,
    TitleCasePipe,
    WritingStylePipe,
    NgStyle,
    ColorPickerDirective,
    NgbNav,
    NgbNavItem,
    NgbNavLinkBase,
    NgbNavContent,
    NgbNavOutlet,
    LoadingComponent
  ],
  templateUrl: './manage-reading-profiles.component.html',
  styleUrl: './manage-reading-profiles.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageReadingProfilesComponent implements OnInit {

  virtualScrollerBreakPoint = 20;

  fontFamilies: Array<string> = [];
  readingProfiles: ReadingProfile[] = [];
  user!: User;
  activeTabId = TabId.ImageReader;
  loading = true;

  selectedProfile: ReadingProfile | null = null;
  readingProfileForm: FormGroup | null = null;
  bookColorThemesTranslated = bookColorThemes.map(o => {
    const d = {...o};
    d.name = translate('theme.' + d.translationKey);
    return d;
  });

  constructor(
    private readingProfileService: ReadingProfileService,
    private cdRef: ChangeDetectorRef,
    private accountService: AccountService,
    private bookService: BookService,
    private destroyRef: DestroyRef,
  ) {
    this.fontFamilies = this.bookService.getFontFamilies().map(f => f.title);
    this.cdRef.markForCheck();
  }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.user = user;
        console.log(this.user.preferences.defaultReadingProfileId);
      }
    });

    this.readingProfileService.all().subscribe(profiles => {
      this.readingProfiles = profiles;
      this.loading = false;
      this.setupForm();
      this.cdRef.markForCheck();
    });

  }

  delete(id: number) {
    this.readingProfileService.delete(id).subscribe(() => {
      this.selectProfile(undefined);
      this.readingProfiles = this.readingProfiles.filter(o => o.id !== id);
      this.cdRef.markForCheck();
    });
  }

  setDefault(id: number) {
    this.readingProfileService.setDefault(id).subscribe(() => {
      this.user.preferences.defaultReadingProfileId = id;
      this.cdRef.markForCheck();
    })
  }

  setupForm() {
    if (this.selectedProfile == null) {
      return;
    }


    this.readingProfileForm = new FormGroup({})

    if (this.fontFamilies.indexOf(this.selectedProfile.bookReaderFontFamily) < 0) {
      this.selectedProfile.bookReaderFontFamily = 'default';
    }

    this.readingProfileForm.addControl('name', new FormControl(this.selectedProfile.name, Validators.required));
    this.readingProfileForm.addControl('readingDirection', new FormControl(this.selectedProfile.readingDirection, []));
    this.readingProfileForm.addControl('scalingOption', new FormControl(this.selectedProfile.scalingOption, []));
    this.readingProfileForm.addControl('pageSplitOption', new FormControl(this.selectedProfile.pageSplitOption, []));
    this.readingProfileForm.addControl('autoCloseMenu', new FormControl(this.selectedProfile.autoCloseMenu, []));
    this.readingProfileForm.addControl('showScreenHints', new FormControl(this.selectedProfile.showScreenHints, []));
    this.readingProfileForm.addControl('readerMode', new FormControl(this.selectedProfile.readerMode, []));
    this.readingProfileForm.addControl('layoutMode', new FormControl(this.selectedProfile.layoutMode, []));
    this.readingProfileForm.addControl('emulateBook', new FormControl(this.selectedProfile.emulateBook, []));
    this.readingProfileForm.addControl('swipeToPaginate', new FormControl(this.selectedProfile.swipeToPaginate, []));
    this.readingProfileForm.addControl('backgroundColor', new FormControl(this.selectedProfile.backgroundColor, []));
    this.readingProfileForm.addControl('allowAutomaticWebtoonReaderDetection', new FormControl(this.selectedProfile.allowAutomaticWebtoonReaderDetection, []));

    this.readingProfileForm.addControl('bookReaderFontFamily', new FormControl(this.selectedProfile.bookReaderFontFamily, []));
    this.readingProfileForm.addControl('bookReaderFontSize', new FormControl(this.selectedProfile.bookReaderFontSize, []));
    this.readingProfileForm.addControl('bookReaderLineSpacing', new FormControl(this.selectedProfile.bookReaderLineSpacing, []));
    this.readingProfileForm.addControl('bookReaderMargin', new FormControl(this.selectedProfile.bookReaderMargin, []));
    this.readingProfileForm.addControl('bookReaderReadingDirection', new FormControl(this.selectedProfile.bookReaderReadingDirection, []));
    this.readingProfileForm.addControl('bookReaderWritingStyle', new FormControl(this.selectedProfile.bookReaderWritingStyle, []))
    this.readingProfileForm.addControl('bookReaderTapToPaginate', new FormControl(this.selectedProfile.bookReaderTapToPaginate, []));
    this.readingProfileForm.addControl('bookReaderLayoutMode', new FormControl(this.selectedProfile.bookReaderLayoutMode || BookPageLayoutMode.Default, []));
    this.readingProfileForm.addControl('bookReaderThemeName', new FormControl(this.selectedProfile.bookReaderThemeName || bookColorThemes[0].name, []));
    this.readingProfileForm.addControl('bookReaderImmersiveMode', new FormControl(this.selectedProfile.bookReaderImmersiveMode, []));

    this.readingProfileForm.addControl('pdfTheme', new FormControl(this.selectedProfile.pdfTheme || PdfTheme.Dark, []));
    this.readingProfileForm.addControl('pdfScrollMode', new FormControl(this.selectedProfile.pdfScrollMode || PdfScrollMode.Vertical, []));
    this.readingProfileForm.addControl('pdfSpreadMode', new FormControl(this.selectedProfile.pdfSpreadMode || PdfSpreadMode.None, []));

    this.readingProfileForm.valueChanges.pipe(
      debounceTime(500),
      distinctUntilChanged(),
      filter(_ => this.readingProfileForm!.valid),
      takeUntilDestroyed(this.destroyRef),
      tap(_ => {
        if (this.selectedProfile!.id == 0) {
          this.readingProfileService.createProfile(this.packData()).subscribe({
            next: createdProfile => {
              this.selectedProfile = createdProfile;
              this.readingProfiles.push(createdProfile);
              this.cdRef.markForCheck();
            },
            error: err => {
              console.log(err);
            }
          })
        } else {
          const profile = this.packData();
          this.readingProfileService.updateProfile(profile).subscribe({
            next: _ => {
              this.readingProfiles = this.readingProfiles.map(p => {
                if (p.id !== profile.id) return p;
                return profile;
              });
              this.cdRef.markForCheck();
            },
            error: err => {
              console.log(err);
            }
          })
        }
      }),
    ).subscribe();
  }

  private packData(): ReadingProfile {
    const data: ReadingProfile = this.readingProfileForm!.getRawValue();
    data.id = this.selectedProfile!.id;
    return data;
  }

  handleBackgroundColorChange(color: string) {
    if (!this.readingProfileForm || !this.selectedProfile) return;

    this.readingProfileForm.markAsDirty();
    this.readingProfileForm.markAsTouched();
    this.selectedProfile.backgroundColor = color;
    this.readingProfileForm.get('backgroundColor')?.setValue(color);
    this.cdRef.markForCheck();
  }

  selectProfile(profile: ReadingProfile | undefined | null) {
    if (profile === undefined) {
      this.selectedProfile = null;
      this.cdRef.markForCheck();
      return;
    }

    this.selectedProfile = profile;
    this.setupForm();
    this.cdRef.markForCheck();
  }

  addNew() {
    const defaultProfile = this.readingProfiles.find(f => f.id === this.user.preferences.defaultReadingProfileId);
    this.selectedProfile = {...defaultProfile!};
    this.selectedProfile.id = 0;
    this.selectedProfile.name = "New Profile #" + (this.readingProfiles.length + 1);
    this.setupForm();
    this.cdRef.markForCheck();
  }

  protected readonly readingDirections = readingDirections;
  protected readonly pdfSpreadModes = pdfSpreadModes;
  protected readonly pageSplitOptions = pageSplitOptions;
  protected readonly bookLayoutModes = bookLayoutModes;
  protected readonly pdfThemes = pdfThemes;
  protected readonly scalingOptions = scalingOptions;
  protected readonly layoutModes = layoutModes;
  protected readonly readerModes = readingModes;
  protected readonly bookWritingStyles = bookWritingStyles;
  protected readonly pdfScrollModes = pdfScrollModes;

  protected readonly TabId = TabId;
}
