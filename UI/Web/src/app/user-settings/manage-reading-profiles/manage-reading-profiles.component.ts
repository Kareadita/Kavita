import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  effect,
  inject,
  OnInit,
  signal
} from '@angular/core';
import {ReadingProfileService} from "../../_services/reading-profile.service";
import {
  bookLayoutModes,
  bookWritingStyles,
  breakPoints,
  layoutModes,
  pageSplitOptions,
  pdfScrollModes,
  pdfSpreadModes,
  pdfThemes,
  readingDirections,
  readingModes,
  ReadingProfile,
  ReadingProfileKind,
  scalingOptions
} from "../../_models/preferences/reading-profiles";
import {translate, TranslocoDirective, TranslocoService} from "@jsverse/transloco";
import {NgStyle, NgTemplateOutlet, TitleCasePipe} from "@angular/common";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";
import {User} from "../../_models/user/user";
import {AccountService} from "../../_services/account.service";
import {debounceTime, distinctUntilChanged, tap} from "rxjs/operators";
import {SentenceCasePipe} from "../../_pipes/sentence-case.pipe";
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators} from "@angular/forms";
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
import {NgbNav, NgbNavContent, NgbNavItem, NgbNavLinkBase, NgbNavOutlet, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {catchError, filter, forkJoin, of, switchMap} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {ToastrService} from "ngx-toastr";
import {ConfirmService} from "../../shared/confirm.service";
import {WikiLink} from "../../_models/wiki";
import {BreakpointPipe} from "../../_pipes/breakpoint.pipe";
import {
  SettingColorPickerComponent
} from "../../settings/_components/setting-colour-picker/setting-color-picker.component";
import {ColorscapeService} from "../../_services/colorscape.service";
import {Color} from "@iplab/ngx-color-picker";
import {FontService} from "../../_services/font.service";
import {EpubFont} from "../../_models/preferences/epub-font";

enum TabId {
  ImageReader = "image-reader",
  BookReader = "book-reader",
  PdfReader = "pdf-reader",
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
    NgbNav,
    NgbNavItem,
    NgbNavLinkBase,
    NgbNavContent,
    NgbNavOutlet,
    LoadingComponent,
    NgbTooltip,
    BreakpointPipe,
    SettingColorPickerComponent
  ],
  templateUrl: './manage-reading-profiles.component.html',
  styleUrl: './manage-reading-profiles.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageReadingProfilesComponent implements OnInit {

  private readonly readingProfileService = inject(ReadingProfileService);
  protected readonly colorscapeService = inject(ColorscapeService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly accountService = inject(AccountService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly toastr = inject(ToastrService);
  private readonly confirmService = inject(ConfirmService);
  private readonly transLoco = inject(TranslocoService);
  private readonly fontService = inject(FontService);

  virtualScrollerBreakPoint = 20;

  savingProfile = signal(false);
  fonts = signal<EpubFont[]>([]);

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

  constructor() {
    effect(() => {
      const user = this.accountService.currentUserSignal();
      if (user) {
        this.user = user;
      }
    });
  }

  ngOnInit(): void {
    forkJoin([
      this.fontService.getFonts(),
      this.readingProfileService.getAllProfiles()
    ]).subscribe(([fonts, profiles]) => {
      this.fonts.set(fonts);

      this.readingProfiles = profiles;
      this.loading = false;
      this.setupForm();

      const defaultProfile = this.readingProfiles.find(rp => rp.kind === ReadingProfileKind.Default);
      this.selectProfile(defaultProfile);

      this.cdRef.markForCheck();
    });
  }

  async delete(readingProfile: ReadingProfile) {
    if (!await this.confirmService.confirm(this.transLoco.translate("manage-reading-profiles.confirm", {name: readingProfile.name}))) {
      return;
    }


    this.readingProfileService.delete(readingProfile.id).subscribe(() => {
      this.selectProfile(undefined);
      this.readingProfiles = this.readingProfiles.filter(o => o.id !== readingProfile.id);
      this.cdRef.markForCheck();
    });
  }

  get widthOverrideLabel() {
    const rawVal = this.readingProfileForm?.get('widthOverride')!.value;
    if (!rawVal) {
      return translate('reader-settings.off');
    }

    const val = parseInt(rawVal);
    return (val <= 0) ? '' : val + '%'
  }

  async setupForm() {
    if (this.selectedProfile == null) {
      return;
    }


    this.readingProfileForm = new FormGroup({})

    if (this.fonts().find(font => font.name === this.selectedProfile?.bookReaderFontFamily) === undefined) {
      this.selectedProfile.bookReaderFontFamily = FontService.DefaultEpubFont;
    }

    this.readingProfileForm.addControl('name', new FormControl(this.selectedProfile.name, Validators.required));


    // Image reader
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
    this.readingProfileForm.addControl('widthOverride', new FormControl(this.selectedProfile.widthOverride, [Validators.min(0), Validators.max(100)]));
    this.readingProfileForm.addControl('disableWidthOverride', new FormControl(this.selectedProfile.disableWidthOverride, []))

    // Epub reader
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

    // Pdf reader
    this.readingProfileForm.addControl('pdfTheme', new FormControl(this.selectedProfile.pdfTheme || PdfTheme.Dark, []));
    this.readingProfileForm.addControl('pdfScrollMode', new FormControl(this.selectedProfile.pdfScrollMode || PdfScrollMode.Vertical, []));
    this.readingProfileForm.addControl('pdfSpreadMode', new FormControl(this.selectedProfile.pdfSpreadMode || PdfSpreadMode.None, []));

    // Auto save
    this.readingProfileForm.valueChanges.pipe(
      debounceTime(500),
      distinctUntilChanged(),
      filter(_ => !this.savingProfile()),
      filter(_ => this.readingProfileForm!.valid),
      takeUntilDestroyed(this.destroyRef),
      tap(_ => this.savingProfile.set(true)),
      switchMap(_ => this.autoSave()),
      tap(() => this.savingProfile.set(false))
    ).subscribe();
  }

  private autoSave() {
    if (this.selectedProfile!.id == 0) {
      return this.readingProfileService.createProfile(this.packData()).pipe(
        tap(createdProfile => {
          this.selectedProfile = createdProfile;
          this.readingProfiles.push(createdProfile);
          this.cdRef.markForCheck();
        }),
        catchError(err => {
          console.log(err);
          this.toastr.error(err.message);

          return of(null);
        })
      );
    }

    const profile = this.packData();
    return this.readingProfileService.updateProfile(profile).pipe(
      tap(newProfile => {
        this.readingProfiles = this.readingProfiles.map(p => {
          if (p.id !== profile.id) return p;

          return newProfile;
        });
        this.cdRef.markForCheck();
      }),
      catchError(err => {
        console.log(err);
        this.toastr.error(err.message);

        return of(null);
      })
    );
  }

  private packData(): ReadingProfile {
    const data: ReadingProfile = this.readingProfileForm!.getRawValue();
    data.id = this.selectedProfile!.id;
    data.readingDirection = parseInt(data.readingDirection + '');
    data.scalingOption = parseInt(data.scalingOption + '');
    data.pageSplitOption = parseInt(data.pageSplitOption + '');
    data.readerMode = parseInt(data.readerMode + '');
    data.layoutMode = parseInt(data.layoutMode + '');
    data.disableWidthOverride = parseInt(data.disableWidthOverride + '');

    data.bookReaderReadingDirection = parseInt(data.bookReaderReadingDirection + '');
    data.bookReaderWritingStyle = parseInt(data.bookReaderWritingStyle + '');
    data.bookReaderLayoutMode = parseInt(data.bookReaderLayoutMode + '');

    data.pdfTheme = parseInt(data.pdfTheme + '');
    data.pdfScrollMode = parseInt(data.pdfScrollMode + '');
    data.pdfSpreadMode = parseInt(data.pdfSpreadMode + '');

    return data;
  }

  handleBackgroundColorChange(color: Color) {
    if (!this.readingProfileForm || !this.selectedProfile) return;

    this.readingProfileForm.markAsDirty();
    this.readingProfileForm.markAsTouched();
    this.selectedProfile.backgroundColor = color.toHexString();
    this.readingProfileForm.get('backgroundColor')?.setValue(color.toHexString());
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
    const defaultProfile = this.readingProfiles.find(f => f.kind === ReadingProfileKind.Default);
    this.selectedProfile = {...defaultProfile!};
    this.selectedProfile.kind = ReadingProfileKind.User;
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
  protected readonly ReadingProfileKind = ReadingProfileKind;
  protected readonly WikiLink = WikiLink;
  protected readonly breakPoints = breakPoints;
}
