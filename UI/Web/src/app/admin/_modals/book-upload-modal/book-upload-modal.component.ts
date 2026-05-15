import {ChangeDetectionStrategy, Component, Input, OnInit, inject, signal} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {NgbActiveModal} from '@ng-bootstrap/ng-bootstrap';
import {translate, TranslocoDirective} from '@jsverse/transloco';
import {ToastrService} from 'ngx-toastr';
import {finalize} from 'rxjs';
import {FileSystemFileEntry, NgxFileDropEntry} from 'ngx-file-drop';
import {Library} from '../../../_models/library/library';
import {BookUploadConflictMode} from '../../../_models/book-upload/book-upload-conflict-mode.enum';
import {BookUploadOptions} from '../../../_models/book-upload/book-upload-options';
import {BookUploadResponse} from '../../../_models/book-upload/book-upload-result';
import {BookUploadService} from '../../../_services/book-upload.service';
import {
  FileDragAndDropUploadComponent
} from '../../../shared/file-drag-and-drop-upload/file-drag-and-drop-upload.component';
import {LoadingComponent} from '../../../shared/loading/loading.component';
import {modalSaved} from '../../../_models/modal/modal-result';

@Component({
  selector: 'app-book-upload-modal',
  imports: [
    TranslocoDirective,
    ReactiveFormsModule,
    FileDragAndDropUploadComponent,
    LoadingComponent,
  ],
  templateUrl: './book-upload-modal.component.html',
  styleUrl: './book-upload-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BookUploadModalComponent implements OnInit {
  private readonly modal = inject(NgbActiveModal);
  private readonly bookUploadService = inject(BookUploadService);
  private readonly toastr = inject(ToastrService);

  @Input() library?: Library;
  @Input() libraries: Library[] = [];

  options = signal<BookUploadOptions | null>(null);
  selectedFiles = signal<File[]>([]);
  uploadResult = signal<BookUploadResponse | null>(null);
  loadingOptions = signal(false);
  uploading = signal(false);

  form = new FormGroup({
    libraryId: new FormControl<number | null>(null, Validators.required),
    libraryFolder: new FormControl('', {nonNullable: true, validators: [Validators.required]}),
    targetFolderName: new FormControl('', {nonNullable: true}),
    conflictMode: new FormControl(BookUploadConflictMode.Reject, {nonNullable: true}),
  });

  ngOnInit() {
    const defaultLibrary = this.library ?? (this.libraries.length === 1 ? this.libraries[0] : undefined);
    if (defaultLibrary) {
      this.form.controls.libraryId.setValue(defaultLibrary.id);
      this.loadOptions(defaultLibrary.id);
    }
  }

  dismiss() {
    this.modal.dismiss();
  }

  onLibraryChange() {
    const libraryId = this.form.controls.libraryId.value;
    this.options.set(null);
    this.form.controls.libraryFolder.setValue('');

    if (libraryId == null) return;
    this.loadOptions(libraryId);
  }

  dropped(entries: NgxFileDropEntry[]) {
    for (const droppedFile of entries) {
      if (!droppedFile.fileEntry.isFile) continue;

      const fileEntry = droppedFile.fileEntry as FileSystemFileEntry;
      fileEntry.file((file: File) => {
        this.addFiles([file], this.getDefaultTargetFolderFromPath(droppedFile.relativePath, file.name));
      });
    }
  }

  filesSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    this.addFiles(Array.from(input.files ?? []));
    input.value = '';
  }

  removeFile(index: number) {
    this.selectedFiles.update(files => files.filter((_, fileIndex) => fileIndex !== index));
  }

  upload() {
    if (this.form.invalid || this.selectedFiles().length === 0) {
      this.toastr.error(translate('book-upload-modal.select-files'));
      return;
    }

    const value = this.form.getRawValue();
    this.uploading.set(true);
    this.uploadResult.set(null);

    this.bookUploadService.uploadFiles({
      libraryId: value.libraryId!,
      libraryFolder: value.libraryFolder,
      targetFolderName: value.targetFolderName,
      conflictMode: value.conflictMode,
    }, this.selectedFiles())
      .pipe(finalize(() => this.uploading.set(false)))
      .subscribe(result => {
        this.uploadResult.set(result);

        if (result.success) {
          this.toastr.success(translate('book-upload-modal.upload-complete'));
          this.modal.close(modalSaved(result));
          return;
        }

        if (result.files.some(file => file.success)) {
          this.toastr.warning(translate('book-upload-modal.upload-partial'));
          return;
        }

        this.toastr.error(translate('book-upload-modal.upload-failed'));
      });
  }

  acceptableExtensions() {
    return this.options()?.acceptableExtensions.join(',') || '.cbz,.zip,.rar,.cbr,.7z,.epub,.pdf';
  }

  displayFileName(file: File) {
    return this.getRelativePath(file) || file.name;
  }

  private loadOptions(libraryId: number) {
    this.loadingOptions.set(true);
    this.bookUploadService.getOptions(libraryId)
      .pipe(finalize(() => this.loadingOptions.set(false)))
      .subscribe({
        next: options => {
          this.options.set(options);
          this.form.controls.libraryFolder.setValue(options.libraryFolders[0] ?? '');
        },
        error: () => {
          this.options.set(null);
          this.form.controls.libraryFolder.setValue('');
          this.toastr.error(translate('book-upload-modal.load-options-failed'));
        }
      });
  }

  private addFiles(files: File[], defaultTargetFolder?: string) {
    if (files.length === 0) return;

    const existingFiles = this.selectedFiles();
    const existingKeys = new Set(existingFiles.map(file => this.getFileKey(file)));
    const nextFiles = [...existingFiles];

    for (const file of files) {
      const key = this.getFileKey(file);
      if (existingKeys.has(key)) continue;

      nextFiles.push(file);
      existingKeys.add(key);
    }

    this.selectedFiles.set(nextFiles);

    if (!this.form.controls.targetFolderName.value && nextFiles.length > 0) {
      this.form.controls.targetFolderName.setValue(defaultTargetFolder ?? this.getDefaultTargetFolderFromFile(files[0]));
    }
  }

  private getDefaultTargetFolderFromFile(file: File) {
    return this.getDefaultTargetFolderFromPath(this.getRelativePath(file), file.name);
  }

  private getDefaultTargetFolderFromPath(path: string | undefined, fallbackFileName: string) {
    const firstSegment = path?.split(/[\\/]/).find(part => part.trim().length > 0);
    if (firstSegment && firstSegment !== fallbackFileName) return firstSegment;

    return this.getDefaultTargetFolder(fallbackFileName);
  }

  private getDefaultTargetFolder(fileName: string) {
    return fileName.replace(/\.(tar\.gz|cbz|zip|rar|cbr|7zip|7z|cb7|cbt|epub|pdf)$/i, '');
  }

  private getFileKey(file: File) {
    return `${this.getRelativePath(file)}:${file.name}:${file.size}:${file.lastModified}`;
  }

  private getRelativePath(file: File) {
    return (file as File & {webkitRelativePath?: string}).webkitRelativePath ?? '';
  }

  protected readonly BookUploadConflictMode = BookUploadConflictMode;
}
