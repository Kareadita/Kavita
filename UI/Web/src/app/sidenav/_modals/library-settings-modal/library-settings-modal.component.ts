import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  Input,
  OnDestroy,
  OnInit
} from '@angular/core';
import { FormGroup, FormControl, Validators } from '@angular/forms';
import { NgbActiveModal, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { debounceTime, distinctUntilChanged, Subject, switchMap, takeUntil, tap } from 'rxjs';
import { SettingsService } from 'src/app/admin/settings.service';
import { DirectoryPickerComponent, DirectoryPickerResult } from 'src/app/admin/_modals/directory-picker/directory-picker.component';
import { ConfirmService } from 'src/app/shared/confirm.service';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import { Library, LibraryType } from 'src/app/_models/library';
import { ImageService } from 'src/app/_services/image.service';
import { LibraryService } from 'src/app/_services/library.service';
import { UploadService } from 'src/app/_services/upload.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

enum TabID {
  General = 'General',
  Folder = 'Folder',
  Cover = 'Cover',
  Advanced = 'Advanced'
}

enum StepID {
  General = 0,
  Folder = 1,
  Cover = 2,
  Advanced = 3
}

@Component({
  selector: 'app-library-settings-modal',
  templateUrl: './library-settings-modal.component.html',
  styleUrls: ['./library-settings-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LibrarySettingsModalComponent implements OnInit {

  @Input({required: true}) library!: Library;
  private readonly destroyRef = inject(DestroyRef);

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
    collapseSeriesRelationships: new FormControl<boolean>(false, { nonNullable: true, validators: [Validators.required] }),
  });

  selectedFolders: string[] = [];
  madeChanges = false;
  libraryTypes: string[] = []

  isAddLibrary = false;
  setupStep = StepID.General;

  get Breakpoint() { return Breakpoint; }
  get TabID() { return TabID; }
  get StepID() { return StepID; }

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
    this.libraryService.scan(this.library.id, true).subscribe(() => this.toastr.info('A forced scan has been started for ' + this.library.name));
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
        if (!await this.confirmService.confirm(`Changing library type will trigger a new scan with different parsing rules and may lead to
        series being re-created and hence you may loose progress and bookmarks. You should backup before you do this. Are you sure you want to continue?`)) return;
      }

      this.libraryService.update(model).subscribe(() => {
        this.close(true);
      });
    } else {
      model.folders = model.folders.map((item: string) => item.startsWith('\\') ? item.substr(1, item.length) : item);
      model.type = parseInt(model.type, 10);
      this.libraryService.create(model).subscribe(() => {
        this.toastr.success('Library created successfully. A scan has been started.');
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
