import {ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal} from '@angular/core';
import {HttpClient, HttpErrorResponse, HttpEventType} from "@angular/common/http";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ToastrService} from "@openng/ngx-toastr";
import {NgxFileDropEntry} from "ngx-file-drop";
import {environment} from "../../../../environments/environment";
import {Library} from "../../../_models/library/library";
import {LibraryService} from "../../../_services/library.service";
import {FileDragAndDropUploadComponent} from "../../../shared/file-drag-and-drop-upload/file-drag-and-drop-upload.component";

/**
 * Iterverse addition: admin upload of books straight into a library folder (see BookUploadController).
 */

type UploadStatus = 'pending' | 'uploading' | 'done' | 'error';

interface QueuedFile {
  file: File;
  status: UploadStatus;
  progress: number;
  error?: string;
}

/** Cloudflare (which fronts library.iterverse.net) rejects request bodies above this */
export const PROXY_UPLOAD_LIMIT_BYTES = 100 * 1024 * 1024;

export const BOOK_UPLOAD_EXTENSIONS = ['.epub', '.pdf', '.cbz', '.cbr', '.zip', '.rar', '.7z', '.cb7', '.cbt'];

@Component({
  selector: 'app-upload-books-modal',
  imports: [TranslocoDirective, FileDragAndDropUploadComponent],
  templateUrl: './upload-books-modal.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UploadBooksModalComponent implements OnInit {
  protected readonly modal = inject(NgbActiveModal);
  private readonly httpClient = inject(HttpClient);
  private readonly libraryService = inject(LibraryService);
  private readonly toastr = inject(ToastrService);
  private readonly baseUrl = environment.apiUrl;

  libraries = input<Library[]>([]);

  libraryId = signal<number | null>(null);
  folderPath = signal('');
  /** When true every file gets its own folder named after the file, otherwise all go into sharedFolder */
  folderPerFile = signal(true);
  sharedFolder = signal('');
  queue = signal<QueuedFile[]>([]);
  uploading = signal(false);

  selectedLibrary = computed(() => this.libraries().find(l => l.id === this.libraryId()));
  hasPending = computed(() => this.queue().some(q => q.status === 'pending'));
  canUpload = computed(() => !this.uploading() && this.hasPending() && !!this.folderPath()
    && (this.folderPerFile() || this.sharedFolder().trim().length > 0));

  protected readonly proxyLimit = PROXY_UPLOAD_LIMIT_BYTES;
  protected readonly acceptedExtensions = BOOK_UPLOAD_EXTENSIONS.join(',');

  ngOnInit() {
    if (this.libraries().length === 1) this.selectLibrary(this.libraries()[0].id);
  }

  selectLibrary(id: number | null) {
    this.libraryId.set(id);
    const folders = this.selectedLibrary()?.folders ?? [];
    this.folderPath.set(folders.length > 0 ? folders[0] : '');
  }

  dropped(entries: NgxFileDropEntry[]) {
    for (const entry of entries) {
      if (!entry.fileEntry.isFile) continue;
      (entry.fileEntry as FileSystemFileEntry).file(file => this.addFile(file));
    }
  }

  private addFile(file: File) {
    const ext = file.name.substring(file.name.lastIndexOf('.')).toLowerCase();
    if (!BOOK_UPLOAD_EXTENSIONS.includes(ext)) {
      this.toastr.warning(translate('upload-books-modal.unsupported', {name: file.name}));
      return;
    }
    if (this.queue().some(q => q.file.name === file.name && q.file.size === file.size)) return;
    this.queue.update(q => [...q, {file, status: 'pending', progress: 0}]);
  }

  remove(item: QueuedFile) {
    this.queue.update(q => q.filter(i => i !== item));
  }

  folderFor(file: File) {
    if (!this.folderPerFile()) return this.sharedFolder().trim();
    const dot = file.name.lastIndexOf('.');
    return dot > 0 ? file.name.substring(0, dot) : file.name;
  }

  async upload() {
    const libraryId = this.libraryId();
    if (!this.canUpload() || libraryId === null) return;

    this.uploading.set(true);
    let uploaded = 0;
    for (const item of this.queue().filter(q => q.status === 'pending')) {
      if (await this.uploadOne(libraryId, item)) uploaded++;
    }
    this.uploading.set(false);

    if (uploaded > 0) {
      // One scan for the whole batch; Kavita skips unchanged folders so this is cheap
      this.libraryService.scan(libraryId).subscribe();
      this.toastr.success(translate('upload-books-modal.uploaded', {count: uploaded}));
    }
  }

  private uploadOne(libraryId: number, item: QueuedFile) {
    const formData = new FormData();
    formData.append('file', item.file, item.file.name);
    const params = {libraryId, folderPath: this.folderPath(), seriesFolder: this.folderFor(item.file)};

    this.setItem(item, {status: 'uploading', progress: 0});
    return new Promise<boolean>(resolve => {
      this.httpClient.post(this.baseUrl + 'bookupload', formData,
        {params, reportProgress: true, observe: 'events', responseType: 'text'}).subscribe({
        next: event => {
          if (event.type === HttpEventType.UploadProgress && event.total) {
            this.setItem(item, {progress: Math.round(100 * event.loaded / event.total)});
          }
        },
        complete: () => {
          this.setItem(item, {status: 'done', progress: 100});
          resolve(true);
        },
        error: (err: HttpErrorResponse) => {
          const message = typeof err.error === 'string' && err.error.length < 300 ? err.error
            : err.status === 413 ? translate('upload-books-modal.too-large') : err.statusText;
          this.setItem(item, {status: 'error', error: message});
          resolve(false);
        }
      });
    });
  }

  private setItem(item: QueuedFile, changes: Partial<QueuedFile>) {
    Object.assign(item, changes);
    this.queue.update(q => [...q]);
  }

  formatSize(bytes: number) {
    return bytes >= 1_048_576 ? (bytes / 1_048_576).toFixed(1) + ' MB' : Math.ceil(bytes / 1024) + ' KB';
  }
}
