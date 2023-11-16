import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  Input,
  OnInit
} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {
  NgbActiveModal,
  NgbModal,
  NgbModalModule,
  NgbNav,
  NgbNavContent,
  NgbNavItem,
  NgbNavLink,
  NgbNavOutlet,
  NgbTooltip
} from '@ng-bootstrap/ng-bootstrap';
import {ToastrService} from 'ngx-toastr';
import {debounceTime, distinctUntilChanged, switchMap, tap} from 'rxjs';
import {SettingsService} from 'src/app/admin/settings.service';
import {
  DirectoryPickerComponent,
  DirectoryPickerResult
} from 'src/app/admin/_modals/directory-picker/directory-picker.component';
import {ConfirmService} from 'src/app/shared/confirm.service';
import {Breakpoint, UtilityService} from 'src/app/shared/_services/utility.service';
import {Library, LibraryType} from 'src/app/_models/library/library';
import {ImageService} from 'src/app/_services/image.service';
import {LibraryService} from 'src/app/_services/library.service';
import {UploadService} from 'src/app/_services/upload.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {CommonModule} from "@angular/common";
import {SentenceCasePipe} from "../../../_pipes/sentence-case.pipe";
import {CoverImageChooserComponent} from "../../../cards/cover-image-chooser/cover-image-chooser.component";
import {translate, TranslocoModule} from "@ngneat/transloco";
import {DefaultDatePipe} from "../../../_pipes/default-date.pipe";
import {allFileTypeGroup, FileTypeGroup} from "../../../_models/library/file-type-group.enum";
import {FileTypeGroupPipe} from "../../../_pipes/file-type-group.pipe";

enum TabID {
  General = 'general-tab',
  Folder = 'folder-tab',
  Cover = 'cover-tab',
  Advanced = 'advanced-tab'
}

enum StepID {
  General = 0,
  Folder = 1,
  Cover = 2,
  Advanced = 3
}

@Component({
  selector: 'app-library-settings-modal',
  standalone: true,
  imports: [CommonModule, NgbModalModule, NgbNavLink, NgbNavItem, NgbNavContent, ReactiveFormsModule, NgbTooltip,
    SentenceCasePipe, NgbNav, NgbNavOutlet, CoverImageChooserComponent, TranslocoModule, DefaultDatePipe,
    FileTypeGroupPipe],
  templateUrl: './library-settings-modal.component.html',
  styleUrls: ['./library-settings-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LibrarySettingsModalComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);

  @Input({required: true}) library!: Library;

  active = TabID.General;
  imageUrls: Array<string> = [];

  libraryForm: FormGroup = new FormGroup({
    name: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    type: new FormControl<LibraryType>(LibraryType.Manga, { nonNullable: true, validators: [Validators.required] }),
    folderWatching: new FormControl<boolean>(true, { nonNullable: true, validators: [Validators.required] }),
    includeInDashboard: new FormControl<boolean>(true, { nonNullable: true, validators: [Validators.required] }),
    includeInRecommended: new FormControl<boolean>(true, { nonNullable: true, validators: [Validators.required] }),
    includeInSearch: new FormControl<boolean>(true, { nonNullable: true, validators: [Validators.required] }),
    manageCollections: new FormControl<boolean>(true, { nonNullable: true, validators: [Validators.required] }),
    manageReadingLists: new FormControl<boolean>(true, { nonNullable: true, validators: [Validators.required] }),
    allowScrobbling: new FormControl<boolean>(true, { nonNullable: true, validators: [Validators.required] }),
    collapseSeriesRelationships: new FormControl<boolean>(false, { nonNullable: true, validators: [Validators.required] }),
  });

  selectedFolders: string[] = [];
  madeChanges = false;
  libraryTypes: string[] = []

  isAddLibrary = false;
  setupStep = StepID.General;
  fileTypeGroups = allFileTypeGroup;

  protected readonly Breakpoint = Breakpoint;
  protected readonly TabID = TabID;

  constructor(public utilityService: UtilityService, private uploadService: UploadService, private modalService: NgbModal,
    private settingService: SettingsService, public modal: NgbActiveModal, private confirmService: ConfirmService,
    private libraryService: LibraryService, private toastr: ToastrService, private readonly cdRef: ChangeDetectorRef,
    private imageService: ImageService) { }

  ngOnInit(): void {

    this.settingService.getLibraryTypes().subscribe((types) => {
      this.libraryTypes = types;
      this.cdRef.markForCheck();
    });


    if (this.library === undefined) {
      this.isAddLibrary = true;
      this.cdRef.markForCheck();
    }

    if (this.library?.coverImage != null && this.library?.coverImage !== '') {
      this.imageUrls.push(this.imageService.getLibraryCoverImage(this.library.id));
      this.cdRef.markForCheck();
    }

    if (this.library && this.library.type === LibraryType.Comic) {
      this.libraryForm.get('allowScrobbling')?.setValue(false);
      this.libraryForm.get('allowScrobbling')?.disable();
    }

    if (this.library) {
      for(let fileTypeGroup of allFileTypeGroup) {
        this.libraryForm.addControl(fileTypeGroup + '', new FormControl(this.library.libraryFileTypes.includes(fileTypeGroup), []));
      }
    } else {
      for(let fileTypeGroup of allFileTypeGroup) {
        this.libraryForm.addControl(fileTypeGroup + '', new FormControl(true, []));
      }
    }


    this.libraryForm.get('name')?.valueChanges.pipe(
      debounceTime(100),
      distinctUntilChanged(),
      switchMap(name => this.libraryService.libraryNameExists(name)),
      tap(exists => {
        const isExistingName = this.libraryForm.get('name')?.value === this.library?.name;
        if (!exists || isExistingName) {
          this.libraryForm.get('name')?.setErrors(null);
        } else {
          this.libraryForm.get('name')?.setErrors({duplicateName: true})
        }
        this.cdRef.markForCheck();
      }),
      takeUntilDestroyed(this.destroyRef)
      ).subscribe();


    this.setValues();

    // This needs to only apply after first render
    this.libraryForm.get('type')?.valueChanges.pipe(

      tap((type: LibraryType) => {
        switch (type) {
          case LibraryType.Manga:
            this.libraryForm.get(FileTypeGroup.Archive + '')?.setValue(true);
            this.libraryForm.get(FileTypeGroup.Images + '')?.setValue(true);
            this.libraryForm.get(FileTypeGroup.Pdf + '')?.setValue(false);
            this.libraryForm.get(FileTypeGroup.Epub + '')?.setValue(false);
            break;
          case LibraryType.Comic:
            this.libraryForm.get(FileTypeGroup.Archive + '')?.setValue(true);
            this.libraryForm.get(FileTypeGroup.Images + '')?.setValue(false);
            this.libraryForm.get(FileTypeGroup.Pdf + '')?.setValue(false);
            this.libraryForm.get(FileTypeGroup.Epub + '')?.setValue(false);
            break;
          case LibraryType.Book:
            this.libraryForm.get(FileTypeGroup.Archive + '')?.setValue(false);
            this.libraryForm.get(FileTypeGroup.Images + '')?.setValue(false);
            this.libraryForm.get(FileTypeGroup.Pdf + '')?.setValue(true);
            this.libraryForm.get(FileTypeGroup.Epub + '')?.setValue(true);
            break;
          case LibraryType.Images:
            this.libraryForm.get(FileTypeGroup.Archive + '')?.setValue(false);
            this.libraryForm.get(FileTypeGroup.Images + '')?.setValue(true);
            this.libraryForm.get(FileTypeGroup.Pdf + '')?.setValue(false);
            this.libraryForm.get(FileTypeGroup.Epub + '')?.setValue(false);

        }
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe();
  }

  setValues() {
    if (this.library !== undefined) {
      this.libraryForm.get('name')?.setValue(this.library.name);
      this.libraryForm.get('type')?.setValue(this.library.type);
      this.libraryForm.get('folderWatching')?.setValue(this.library.folderWatching);
      this.libraryForm.get('includeInDashboard')?.setValue(this.library.includeInDashboard);
      this.libraryForm.get('includeInRecommended')?.setValue(this.library.includeInRecommended);
      this.libraryForm.get('includeInSearch')?.setValue(this.library.includeInSearch);
      this.libraryForm.get('manageCollections')?.setValue(this.library.manageCollections);
      this.libraryForm.get('manageReadingLists')?.setValue(this.library.manageReadingLists);
      this.libraryForm.get('collapseSeriesRelationships')?.setValue(this.library.collapseSeriesRelationships);
      this.libraryForm.get('allowScrobbling')?.setValue(this.library.allowScrobbling);
      this.selectedFolders = this.library.folders;
      this.madeChanges = false;
      this.cdRef.markForCheck();
    }
  }

  isDisabled() {
    return !(this.libraryForm.valid && this.selectedFolders.length > 0);
  }

  reset() {
    this.setValues();
  }

  close(returnVal= false) {
    this.modal.close(returnVal);
  }

  forceScan() {
    this.libraryService.scan(this.library.id, true)
      .subscribe(() => this.toastr.info(translate('toasts.forced-scan-queued', {name: this.library.name})));
  }

  async save() {
    const model = this.libraryForm.value;
    model.folders = this.selectedFolders;

    if (this.libraryForm.errors) {
      return;
    }

    if (this.library !== undefined) {
      model.id = this.library.id;
      model.folders = model.folders.map((item: string) => item.startsWith('\\') ? item.substr(1, item.length) : item);
      model.type = parseInt(model.type, 10);

      if (model.type !== this.library.type) {
        if (!await this.confirmService.confirm(translate('toasts.confirm-library-type-change'))) return;
      }

      this.libraryService.update(model).subscribe(() => {
        this.close(true);
      });
    } else {
      model.folders = model.folders.map((item: string) => item.startsWith('\\') ? item.substr(1, item.length) : item);
      model.type = parseInt(model.type, 10);
      this.libraryService.create(model).subscribe(() => {
        this.toastr.success(translate('toasts.library-created'));
        this.close(true);
      });
    }
  }

  nextStep() {
    this.setupStep++;
    switch(this.setupStep) {
      case StepID.Folder:
        this.active = TabID.Folder;
        break;
      case StepID.Cover:
        this.active = TabID.Cover;
        break;
      case StepID.Advanced:
        this.active = TabID.Advanced;
        break;
    }
    this.cdRef.markForCheck();
  }

  applyCoverImage(coverUrl: string) {
    this.uploadService.updateLibraryCoverImage(this.library.id, coverUrl).subscribe(() => {});
  }

  updateCoverImageIndex(selectedIndex: number) {
    if (selectedIndex <= 0) return;
    this.applyCoverImage(this.imageUrls[selectedIndex]);
  }

  resetCoverImage() {
    this.uploadService.updateLibraryCoverImage(this.library.id, '').subscribe(() => {});
  }

  openDirectoryPicker() {
    const modalRef = this.modalService.open(DirectoryPickerComponent, { scrollable: true, size: 'lg' });
    modalRef.closed.subscribe((closeResult: DirectoryPickerResult) => {
      if (closeResult.success) {
        if (!this.selectedFolders.includes(closeResult.folderPath)) {
          this.selectedFolders.push(closeResult.folderPath);
          this.madeChanges = true;
          this.cdRef.markForCheck();
        }
      }
    });
  }

  removeFolder(folder: string) {
    this.selectedFolders = this.selectedFolders.filter(item => item !== folder);
    this.madeChanges = true;
    this.cdRef.markForCheck();
  }

  isNextDisabled() {
    switch (this.setupStep) {
      case StepID.General:
        return this.libraryForm.get('name')?.invalid || this.libraryForm.get('type')?.invalid;
      case StepID.Folder:
        return this.selectedFolders.length === 0;
      case StepID.Cover:
        return false; // Covers are optional
      case StepID.Advanced:
        return false; // Advanced are optional
    }
  }

}
