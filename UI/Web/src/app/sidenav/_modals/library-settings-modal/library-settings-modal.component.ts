import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  model,
  OnInit,
  signal
} from '@angular/core';
import {
  NgbActiveModal,
  NgbModalModule,
  NgbNav,
  NgbNavContent,
  NgbNavItem,
  NgbNavLink,
  NgbNavOutlet,
  NgbTooltip
} from '@ng-bootstrap/ng-bootstrap';
import {ToastrService} from '@openng/ngx-toastr';
import {skip, tap} from 'rxjs';
import {takeUntilDestroyed, toObservable} from "@angular/core/rxjs-interop";
import {NgTemplateOutlet} from "@angular/common";
import {SentenceCasePipe} from "../../../_pipes/sentence-case.pipe";
import {CoverImageChooserComponent} from "../../../cards/cover-image-chooser/cover-image-chooser.component";
import {
  CoverChooserConfigFactoryService,
  CoverImageChooserConfig
} from "../../../_services/cover-chooser-config-factory.service";
import {translate, TranslocoModule} from "@jsverse/transloco";
import {DefaultDatePipe} from "../../../_pipes/default-date.pipe";
import {allFileTypeGroup, FileTypeGroup} from "../../../_models/library/file-type-group.enum";
import {FileTypeGroupPipe} from "../../../_pipes/file-type-group.pipe";
import {EditListComponent} from "../../../shared/edit-list/edit-list.component";
import {WikiLink} from "../../../_models/wiki";
import {SettingItemComponent} from "../../../settings/_components/setting-item/setting-item.component";
import {SettingSwitchComponent} from "../../../settings/_components/setting-switch/setting-switch.component";
import {SettingButtonComponent} from "../../../settings/_components/setting-button/setting-button.component";
import {LibraryTypePipe} from "../../../_pipes/library-type.pipe";
import {LibraryTypeSubtitlePipe} from "../../../_pipes/library-type-subtitle.pipe";
import {TypeaheadComponent} from "../../../typeahead/_components/typeahead.component";
import {TypeaheadConfig} from "../../../typeahead/_models/typeahead-config";
import {Language} from "../../../_models/metadata/language";
import {BreakpointService} from "../../../_services/breakpoint.service";
import {ActionFactoryService} from "../../../_services/action-factory.service";
import {Action} from "../../../_models/actionables/action";
import {ActionItem} from "../../../_models/actionables/action-item";
import {modalSaved} from "../../../_models/modal/modal-result";
import {ModalService} from "../../../_services/modal.service";
import {Tabs} from "../../../_models/tabs";
import {TabTitlePipe} from "../../../_pipes/tab-title.pipe";
import {MetadataProvider} from "../../../_models/kavitaplus/metadata-provider.enum";
import {MetadataProviderTitlePipe} from "../../../_pipes/metadata-provider-title.pipe";
import {UtcToLocalTimePipe} from "../../../_pipes/utc-to-local-time.pipe";
import {UtilityService} from "../../../shared/_services/utility.service";
import {UploadService} from "../../../_services/upload.service";
import {ConfirmService} from "../../../shared/confirm.service";
import {LibraryService} from "../../../_services/library.service";
import {allLibraryTypes, Library, LibraryType} from "../../../_models/library/library";
import {
  DirectoryPickerModalComponent,
  DirectoryPickerResult
} from "../../../admin/_modals/directory-picker/directory-picker-modal.component";
import {TypeaheadConfigFactoryService} from "../../../typeahead-config-factory.service";
import {FormFieldDirective} from "../../../_directives/form-field.directive";
import {ValidationErrorsComponent} from "../../../shared/_components/validation-errors/validation-errors.component";
import {disabled, form, FormField, required, validate} from "@angular/forms/signals";
import {SettingSelectComponent} from "../../../settings/_components/setting-enum-select/setting-select.component";

enum StepID {
  General = 0,
  Folder = 1,
  Cover = 2,
  Advanced = 3
}

interface FormModel {
  id: number;
  name: string;
  type: LibraryType;
  folderWatching: boolean;
  includeInDashboard: boolean;
  includeInRecommended: boolean;
  includeInSearch: boolean;
  manageCollections: boolean;
  manageReadingLists: boolean;
  allowScrobbling: boolean;
  allowMetadataMatching: boolean;
  collapseSeriesRelationships: boolean;
  enableMetadata: boolean;
  removePrefixForSortName: boolean;
  inheritWebLinksFromFirstChapter: boolean;
  defaultLanguage: string;
  metadataProvider: MetadataProvider;
  excludePatterns: string[];
  folders: string[];
  fileGroupTypes: FileTypeGroup[];
}

@Component({
  selector: 'app-library-settings-modal',
  imports: [NgbModalModule, NgbNavLink, NgbNavItem, NgbNavContent, NgbTooltip,
    SentenceCasePipe, NgbNav, NgbNavOutlet, CoverImageChooserComponent, TranslocoModule, DefaultDatePipe,
    FileTypeGroupPipe, EditListComponent, SettingItemComponent, SettingSwitchComponent, SettingButtonComponent, LibraryTypeSubtitlePipe, NgTemplateOutlet, TypeaheadComponent, TabTitlePipe, MetadataProviderTitlePipe, UtcToLocalTimePipe, FormFieldDirective, ValidationErrorsComponent, FormField, SettingSelectComponent],
  templateUrl: './library-settings-modal.component.html',
  styleUrls: ['./library-settings-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LibrarySettingsModalComponent implements OnInit {

  protected readonly utilityService = inject(UtilityService);
  protected readonly modal = inject(NgbActiveModal);
  private readonly destroyRef = inject(DestroyRef);
  private readonly uploadService = inject(UploadService);
  private readonly modalService = inject(ModalService);
  private readonly confirmService = inject(ConfirmService);
  private readonly libraryService = inject(LibraryService);
  private readonly toastr = inject(ToastrService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly actionFactoryService = inject(ActionFactoryService);
  protected readonly breakpointService = inject(BreakpointService);
  private readonly coverChooserConfigFactory = inject(CoverChooserConfigFactoryService);
  private readonly typeaheadSettingFactoryService = inject(TypeaheadConfigFactoryService);

  protected readonly LibraryType = LibraryType;
  protected readonly Tabs = Tabs;
  protected readonly WikiLink = WikiLink;
  protected readonly Action = Action;
  protected readonly libraryTypePipe = new LibraryTypePipe();

  library = model<Library>();

  active = Tabs.General;
  chooserConfig = signal<CoverImageChooserConfig>({});
  protected readonly excludePatternTooltip = `<span>` + translate('library-settings-modal.exclude-patterns-tooltip') +
  `<a class="ms-1" href="${WikiLink.ScannerExclude}" rel="noopener noreferrer" target="_blank">${translate('library-settings-modal.help')}` +
  `<i class="fa fa-external-link-alt ms-1" aria-hidden="true"></i></a>`;

  formModel = signal<FormModel>({
    id: 0,
    allowMetadataMatching: true,
    allowScrobbling: true,
    collapseSeriesRelationships: false,
    defaultLanguage: "",
    enableMetadata: true,
    excludePatterns: [],
    folderWatching: true,
    folders: [],
    includeInDashboard: true,
    includeInRecommended: true,
    includeInSearch: true,
    inheritWebLinksFromFirstChapter: false,
    fileGroupTypes: this.getLibraryFileTypes(LibraryType.Manga),
    manageCollections: false,
    manageReadingLists: false,
    metadataProvider: MetadataProvider.Mangabaka,
    name: "",
    removePrefixForSortName: false,
    type: LibraryType.Manga
  });
  formGroup = form(this.formModel, path => {
    required(path.name);
    required(path.type);
    disabled(path.allowScrobbling, { when: (ctx) => {
      const libraryType = ctx.valueOf(path.type);
      return !this.scrobbleEnabledLibraries().includes(libraryType);
    }});
    validate(path.name, (ctx) => {
      const name = ctx.valueOf(path.name);
      if (this.library()?.name == name) return null;

      if (this.libraryNames().includes(name)) {
        return {
          kind: 'duplicateName'
        };
      }

      return null;
    });
  });

  isDisabled = computed(() => {
    const hasFolder = this.formGroup.folders().value().length > 0;
    const hasFileType = this.formGroup.fileGroupTypes().value().length > 0;
    return this.formGroup().invalid() || !hasFolder || !hasFileType;
  });

  supportsMetadata = computed(() => {
    if (this.validMetadataProviders.hasValue()) {
      return this.validMetadataProviders.value().length > 0;
    }

    return false;
  });

  libraryNames = signal<string[]>([]);
  scrobbleEnabledLibraries = signal<LibraryType[]>([]);
  validMetadataProviders = this.libraryService.getSupportedMetadataProviders(() => this.formModel().type);

  libraryTypes = allLibraryTypes.map(f => {
    return {title: this.libraryTypePipe.transform(f), value: f};
  }).sort((a, b) => a.title.localeCompare(b.title));

  languageSettings = signal<TypeaheadConfig<Language> | null>(null);

  setupStep = signal<StepID>(StepID.General);
  isAddLibrary= signal<boolean>(true);
  filesAtRoot = signal<Array<string>>([]);

  tasks: ActionItem<Library>[] = this.getTasks();

  constructor() {
    effect(() => {
      if (!this.validMetadataProviders.hasValue()) return;
      const validMetadataProviders = this.validMetadataProviders.value();
      const selectedMetadataProvider = this.formModel().metadataProvider;

      if (!validMetadataProviders.includes(selectedMetadataProvider)) {
        this.formGroup.metadataProvider().value.set(validMetadataProviders[0]);
      }
    });

    toObservable(this.formGroup.type().value).pipe(
      takeUntilDestroyed(),
      skip(1), // Skip setting library values on load
      tap(libraryType => {
        this.formGroup.fileGroupTypes().value.set(this.getLibraryFileTypes(libraryType));

        if (!this.scrobbleEnabledLibraries().includes(libraryType)) {
          this.formGroup.allowScrobbling().value.set(false);
        }
      })
    ).subscribe();
  }

  ngOnInit(): void {
    if (this.library() !== undefined) {
      this.isAddLibrary.set(false);
    }

    this.chooserConfig.set(this.coverChooserConfigFactory.forLibrary(this.library()));

    this.libraryService.getLibraries().pipe(
      tap(libs => this.libraryNames.set(libs.map(l => l.name)))
    ).subscribe();

    this.libraryService.getLibraryTypesWithScrobbleSupport().pipe(
      takeUntilDestroyed(this.destroyRef),
      tap(libraryTypes => {
        this.scrobbleEnabledLibraries.set(libraryTypes);
        // We want scrobbleEnabledLibraries to be loaded before doing this
        this.setValues();
      })
    ).subscribe();

    this.languageSettings.set(this.typeaheadSettingFactoryService.forLanguage({id: 'language', currentSelectedLanguage: this.library()?.defaultLanguage,
      overrides: {
        showLocked: false
      }
    }));
  }

  private getLibraryFileTypes(libType: LibraryType) {
    switch (libType) {
      case LibraryType.Manga:
        return [FileTypeGroup.Archive, FileTypeGroup.Images];
      case LibraryType.Comic:
      case LibraryType.ComicVine:
        return [FileTypeGroup.Archive];
      case LibraryType.Book:
        return [FileTypeGroup.Pdf, FileTypeGroup.Epub];
      case LibraryType.Images:
        return [FileTypeGroup.Images];
      case LibraryType.LightNovel:
        return [FileTypeGroup.Epub];
    }
  }

  setValues() {
    const library = this.library();
    if (library === undefined) {
      return;
    }

    this.formModel.set({
      id: library.id,
      allowMetadataMatching: library.allowMetadataMatching,
      allowScrobbling: library.allowScrobbling,
      collapseSeriesRelationships: library.collapseSeriesRelationships,
      defaultLanguage: library.defaultLanguage,
      enableMetadata: library.enableMetadata,
      excludePatterns: library.excludePatterns,
      folderWatching: library.folderWatching,
      folders: library.folders,
      includeInDashboard: library.includeInDashboard,
      includeInRecommended: library.includeInRecommended,
      includeInSearch: library.includeInSearch,
      inheritWebLinksFromFirstChapter: library.inheritWebLinksFromFirstChapter,
      fileGroupTypes: library.libraryFileTypes,
      manageCollections: library.manageCollections,
      manageReadingLists: library.manageReadingLists,
      metadataProvider: library.metadataProvider,
      name: library.name,
      removePrefixForSortName: library.removePrefixForSortName,
      type: library.type,
    });

    this.checkForFilesAtRoot();
  }

  updateLanguage(languages: Array<Language>) {
    this.formGroup.defaultLanguage().value.set(languages.at(0)?.isoCode ?? '');
  }

  updateGlobs(items: Array<string>) {
    this.formGroup.excludePatterns().value.set(items);
  }

  reset() {
    this.setValues();
  }

  close() {
    this.modal.dismiss();
  }

  forceScan() {
    this.libraryService.scan(this.library()!.id, true)
      .subscribe(() => {
        this.toastr.info(translate('toasts.forced-scan-queued', {name: this.library!.name}));
        this.close();
      });
  }

  async save() {
    if (this.formGroup().invalid()) {
      return;
    }

    const model = this.formModel();
    model.folders = model.folders.map((item: string) => item.startsWith('\\') ? item.substring(1, item.length) : item);

    if (this.library() !== undefined) {
      if (model.type !== this.library()?.type) {
        if (!await this.confirmService.confirm(translate('toasts.confirm-library-type-change'))) return;
      }

      this.libraryService.update(model).subscribe((updatedLib) => {
        this.modal.close(modalSaved(updatedLib));
      });
    } else {
      this.libraryService.create(model).subscribe((lib) => {
        this.toastr.success(translate('toasts.library-created'));
        this.modal.close(modalSaved(lib));
      });
    }
  }

  nextStep() {
    this.setupStep.update(x => x + 1);
    switch(this.setupStep()) {
      case StepID.Folder:
        this.active = Tabs.Folder;
        break;
      case StepID.Cover:
        this.active = Tabs.CoverImage;
        break;
      case StepID.Advanced:
        this.active = Tabs.Advanced;
        break;
    }
    this.cdRef.markForCheck();
  }

  applyCoverImage(coverUrl: string) {
    this.uploadService.updateLibraryCoverImage(this.library()!.id, coverUrl).subscribe();
  }

  handleCoverChanged(event: { isDirty: boolean; fileName: string }) {
    if (event.isDirty) {
      this.applyCoverImage(event.fileName);
    }
  }

  openDirectoryPicker() {
    const modalRef = this.modalService.open(DirectoryPickerModalComponent);
    modalRef.closed.subscribe((closeResult: DirectoryPickerResult) => {
      if (closeResult.success) {
        if (!this.formGroup.folders().value().includes(closeResult.folderPath)) {
          this.formGroup.folders().value.update(x => [...x, closeResult.folderPath]);
          this.checkForFilesAtRoot(true);
        }
      }
    });
  }

  removeFolder(folder: string) {
    this.formGroup.folders().value.update(x => [...x.filter(item => item !== folder)]);
    this.checkForFilesAtRoot();
  }

  handleFileTypeGroupChange($event: Event, group: FileTypeGroup) {
    const enabled = ($event.target as HTMLInputElement).checked;
    if (enabled) {
      this.formGroup.fileGroupTypes().value.update(x => [...x, group]);
      return;
    }

    this.formGroup.fileGroupTypes().value.update(x => [...x.filter(item => item !== group)]);
  }

  handleFileTypeGroupLabelClick(group: FileTypeGroup) {
    const enabled = this.formGroup.fileGroupTypes().value().includes(group);
    if (enabled) {
      this.formGroup.fileGroupTypes().value.update(x => [...x.filter(item => item !== group)]);
      return;
    }

    this.formGroup.fileGroupTypes().value.update(x => [...x, group]);
  }

  isNextDisabled = computed(() => {
    switch (this.setupStep()) {
      case StepID.General:
        return this.formGroup().invalid();
      case StepID.Folder:
        return this.formGroup.folders().value().length === 0;
      case StepID.Cover:
        return false; // Covers are optional
      case StepID.Advanced:
        return false; // Advanced are optional
    }
  })

  getTasks() {
    const blackList = [Action.Edit];
    return this.actionFactoryService.getActionablesForSettingsPage(this.actionFactoryService.getLibraryActions(), blackList);
  }

  runTask(task: ActionItem<Library>) {
    if (task.callback) {
      task.callback(task, this.library()!).subscribe();
    }
  }

  checkForFilesAtRoot(showToast: boolean = false) {
    this.libraryService.hasFilesAtRoot(this.formGroup.folders().value()).subscribe(results => {
      const newValues = results.filter(item => !this.filesAtRoot().includes(item));
      if (showToast && newValues.length > 0) {
        this.toastr.error(translate('library-settings-modal.files-at-root-warning'))
      }

      this.filesAtRoot.set(results);
    })
  }

  protected readonly StepID = StepID;
  protected readonly allFileTypeGroup = allFileTypeGroup;
}
