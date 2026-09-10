import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, computed,
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
import {debounceTime, distinctUntilChanged, map, tap} from "rxjs/operators";
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
import {takeUntilDestroyed, toObservable} from "@angular/core/rxjs-interop";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {ToastrService} from '@openng/ngx-toastr';
import {ConfirmService} from "../../shared/confirm.service";
import {WikiLink} from "../../_models/wiki";
import {BreakpointPipe} from "../../_pipes/breakpoint.pipe";
import {
  SettingColorPickerComponent
} from "../../settings/_components/setting-colour-picker/setting-color-picker.component";
import {ColorscapeService} from "../../_services/colorscape.service";
import {Color} from "@iplab/ngx-color-picker";
import {FontService} from "../../_services/font.service";
import {EpubFont, FontProvider} from "../../_models/preferences/epub-font";
import {DeviceService} from "../../_services/device.service";
import {ModalService} from "../../_services/modal.service";
import {ListSelectModalComponent} from "../../shared/_components/list-select-modal/list-select-modal.component";
import {ClientDevice} from "../../_models/client-device";
import {TabTitlePipe} from "../../_pipes/tab-title.pipe";
import {Tabs} from "../../_models/tabs";
import {EpubFontTitlePipe} from "../../_pipes/epub-font-title.pipe";
import {FormFieldDirective} from "../../_directives/form-field.directive";
import {ReadingDirection} from "../../_models/preferences/reading-direction";
import {WritingStyle} from "../../_models/preferences/writing-style";
import {Breakpoint} from "../../_services/breakpoint.service";
import {LayoutMode} from "../../manga-reader/_models/layout-mode";
import {PageSplitOption} from "../../_models/preferences/page-split-option";
import {ReaderMode} from "../../_models/preferences/reader-mode";
import {ScalingOption} from "../../_models/preferences/scaling-option";
import {form, FormField, max, min} from "@angular/forms/signals";
import {
  EnumOption,
  SettingSelectComponent
} from "../../settings/_components/setting-enum-select/setting-select.component";


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
    SettingColorPickerComponent,
    TabTitlePipe,
    EpubFontTitlePipe, FormFieldDirective, FormField, SettingSelectComponent],
  templateUrl: './manage-reading-profiles.component.html',
  styleUrl: './manage-reading-profiles.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageReadingProfilesComponent implements OnInit {

  private readonly readingProfileService = inject(ReadingProfileService);
  protected readonly colorscapeService = inject(ColorscapeService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly toastr = inject(ToastrService);
  private readonly confirmService = inject(ConfirmService);
  private readonly transLoco = inject(TranslocoService);
  private readonly fontService = inject(FontService);
  private readonly deviceService = inject(DeviceService);
  private readonly modalService = inject(ModalService);

  virtualScrollerBreakPoint = 20;

  loading = signal(true);
  savingProfile = signal(false);
  fonts = signal<EpubFont[]>([]);
  devices = signal<ClientDevice[]>([]);
  readingProfiles = signal<ReadingProfile[]>([]);

  fontEnumOptions = computed<EnumOption<string>[]>(() => this.fonts().map(f => ({value: f.family, label: f.name})));

  activeTabId = Tabs.ImageReader;

  profileSelected = signal(false);
  formModel = signal<ReadingProfile>({
    allowAutomaticWebtoonReaderDetection: false,
    autoCloseMenu: false,
    backgroundColor: "",
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
    bookReaderWritingStyle: WritingStyle.Horizontal,
    deviceIds: [],
    disableWidthOverride: Breakpoint.Mobile,
    emulateBook: false,
    id: 0,
    kind: ReadingProfileKind.Default,
    layoutMode: LayoutMode.Single,
    libraryIds: [],
    name: "",
    pageSplitOption: PageSplitOption.NoSplit,
    pdfScrollMode: PdfScrollMode.Page,
    pdfSpreadMode: PdfSpreadMode.None,
    pdfTheme: PdfTheme.Light,
    readerMode: ReaderMode.LeftRight,
    readingDirection: ReadingDirection.LeftToRight,
    scalingOption: ScalingOption.Automatic,
    seriesIds: [],
    showScreenHints: false,
    swipeToPaginate: false,
    widthOverride: null
  });
  formGroup = form(this.formModel, path => {
    // Need custom maybeMin to allow undefined
    //min(path.widthOverride, 0);
    //max(path.widthOverride, 100)

    min(path.bookReaderFontSize, 50);
    max(path.bookReaderFontSize, 300);
    min(path.bookReaderLineSpacing, 100);
    max(path.bookReaderLineSpacing, 200);
    min(path.bookReaderMargin, 0);
    max(path.bookReaderMargin, 30);
  });

  bookColorThemesTranslated = bookColorThemes.map(o => {
    return {
      ...o,
      value: o.name,
      title: translate('theme.' + o.translationKey)
    };
  });

  constructor() {
    toObservable(this.formModel).pipe(
      debounceTime(500),
      distinctUntilChanged(),
      filter(_ => !this.savingProfile()),
      filter(_ => this.formGroup().valid()),
      takeUntilDestroyed(this.destroyRef),
      tap(_ => this.savingProfile.set(true)),
      switchMap(_ => this.autoSave()),
      tap(() => this.savingProfile.set(false))
    ).subscribe();
  }

  ngOnInit(): void {
    forkJoin([
      this.fontService.getFonts(),
      this.readingProfileService.getAllProfiles(),
      this.deviceService.getMyClientDevices(),
    ]).subscribe(([fonts, profiles, devices]) => {
      this.fonts.set([...new Map(fonts.map(font => [font.family, font])).values()]);
      this.devices.set(devices);

      this.readingProfiles.set(profiles);
      this.loading.set(false);

      const defaultProfile = this.readingProfiles().find(rp => rp.kind === ReadingProfileKind.Default);
      this.selectProfile(defaultProfile);
    });
  }

  async delete(readingProfile: ReadingProfile) {
    if (!await this.confirmService.confirm(this.transLoco.translate("manage-reading-profiles.confirm", {name: readingProfile.name}))) {
      return;
    }

    this.readingProfileService.delete(readingProfile.id).subscribe(() => {
      this.selectProfile(undefined);
      this.readingProfiles.update(x => [...x.filter(o => o.id !== readingProfile.id)]);
    });
  }

  widthOverrideLabel = computed(() => {
    const value = this.formGroup.widthOverride().value();
    if (value === null || value === undefined) {
      return translate('reader-settings.off');
    }

    return (value <= 0) ? '' : value + '%'
  });

  private autoSave() {
    const profile = this.formModel();

    if (profile.id == 0) {
      return this.readingProfileService.createProfile(profile).pipe(
        tap(createdProfile => {
           this.formModel.set(createdProfile);
          this.readingProfiles.update(x => [...x, createdProfile]);
        }),
        catchError(err => {
          console.error(err);
          this.toastr.error(err.message);
          return of(null);
        })
      );
    }

    return this.readingProfileService.updateProfile(profile).pipe(
      tap(newProfile => {
        this.readingProfiles.update(x => [...x.map(p => {
          if (p.id !== profile.id) return p;

          return newProfile;
        })]);
      }),
      catchError(err => {
        console.error(err);
        this.toastr.error(err.message);

        return of(null);
      })
    );
  }

  handleBackgroundColorChange(color: Color) {
    if (this.formModel() == null) return;

    this.formGroup.backgroundColor().value.set(color.toHexString())
  }

  selectProfile(profile: ReadingProfile | undefined | null) {
    if (profile === undefined || profile === null) {
      this.profileSelected.set(false);
      return;
    }

    this.profileSelected.set(true);
    this.formModel.set(profile);
  }

  addNew() {
    const defaultProfile = this.readingProfiles().find(f => f.kind === ReadingProfileKind.Default);
    if (defaultProfile === undefined || defaultProfile === null) {
      throw new Error("No default profile to branch from");
    }

    const newProfile = {...defaultProfile!};
    newProfile.kind = ReadingProfileKind.User;
    newProfile.id = 0;
    newProfile.name = "New Profile #" + (this.readingProfiles().length + 1);

    this.selectProfile(newProfile);
  }

  protected setDevices() {
    if (!this.profileSelected() || this.formModel().id === 0) return;

    const ref = this.modalService.open(ListSelectModalComponent);
    const profileName = this.formModel().name;
    ref.setInput('title', translate('manage-reading-profiles.select-devices-for', {name: profileName}));
    ref.setInput('multiSelect', true);
    ref.setInput('requireConfirmation', true);
    ref.setInput('preSelectedItems', this.formModel().deviceIds ?? []);
    ref.setInput('inputItems', this.devices().map(d => ({
      label: d.friendlyName,
      value: d.id
    })));

    ref.closed.pipe(
      filter(devices => !!devices),
      switchMap((devices: number[]) => {
        return this.readingProfileService.setDevices(this.formModel().id, devices).pipe(map(() => devices))
      }),
      tap(devices => {
        this.formModel.update(x => ({
          ...x,
          deviceIds: devices
        }))
      }),
    ).subscribe();

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
  protected readonly Tabs = Tabs;
  protected readonly ReadingProfileKind = ReadingProfileKind;
  protected readonly WikiLink = WikiLink;
  protected readonly breakPoints = breakPoints;
  protected readonly FontProvider = FontProvider;
  protected readonly form = form;
}
