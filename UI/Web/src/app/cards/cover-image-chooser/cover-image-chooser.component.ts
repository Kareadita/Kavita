import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  EventEmitter,
  inject,
  Inject,
  Input,
  OnInit,
  Output
} from '@angular/core';
import {FormBuilder, FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {NgxFileDropEntry, FileSystemFileEntry, NgxFileDropModule} from 'ngx-file-drop';
import { fromEvent } from 'rxjs';
import { takeWhile } from 'rxjs/operators';
import { ToastrService } from 'ngx-toastr';
import { ImageService } from 'src/app/_services/image.service';
import { KEY_CODES } from 'src/app/shared/_services/utility.service';
import { UploadService } from 'src/app/_services/upload.service';
import {DOCUMENT, NgClass} from '@angular/common';
import {ImageComponent} from "../../shared/image/image.component";
import {translate, TranslocoModule} from "@jsverse/transloco";

@Component({
  selector: 'app-cover-image-chooser',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    NgxFileDropModule,
    ImageComponent,
    TranslocoModule,
    NgClass
  ],
  templateUrl: './cover-image-chooser.component.html',
  styleUrls: ['./cover-image-chooser.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CoverImageChooserComponent implements OnInit {

  private readonly cdRef = inject(ChangeDetectorRef);
  public readonly imageService = inject(ImageService);
  public readonly fb = inject(FormBuilder);
  public readonly toastr = inject(ToastrService);
  public readonly uploadService = inject(UploadService);

  /**
   * If buttons show under images to allow immediate selection of cover images.
   */
  @Input() showApplyButton: boolean = false;
  /**
   * When a cover image is selected, this will be called with a base url representation of the file.
   */
  @Output() applyCover: EventEmitter<string> = new EventEmitter<string>();
  /**
   * When a cover image is reset, this will be called.
   */
  @Output() resetCover: EventEmitter<void> = new EventEmitter<void>();

  @Input() imageUrls: Array<string> = [];
  @Output() imageUrlsChange: EventEmitter<Array<string>> = new EventEmitter<Array<string>>();

  /**
   * Should the control give the ability to select an image that emits the reset status for cover image
   */
  @Input() showReset: boolean = false;
  @Output() resetClicked: EventEmitter<void> = new EventEmitter<void>();

  /**
   * Emits the selected index. Used usually to check if something other than the default image was selected.
   */
  @Output() imageSelected: EventEmitter<number> = new EventEmitter<number>();
  /**
   * Emits a base64 encoded image
   */
  @Output() selectedBase64Url: EventEmitter<string> = new EventEmitter<string>();



  selectedIndex: number = 0;
  /**
   * Only applies for showApplyButton. Used to track which image is applied.
   */
  appliedIndex: number = 0;
  form!: FormGroup;
  files: NgxFileDropEntry[] = [];
  acceptableExtensions = ['.png', '.jpg', '.jpeg', '.gif', '.webp', '.avif'].join(',');
  mode: 'file' | 'url' | 'all' = 'all';

  constructor(@Inject(DOCUMENT) private document: Document) { }

  ngOnInit(): void {
    this.form = this.fb.group({
      coverImageUrl: new FormControl('', [])
    });

    this.cdRef.markForCheck();
  }


  /**
   * Generates a base64 encoding for an Image. Used in manual file upload flow.
   * @param img
   * @returns
   */
  getBase64Image(img: HTMLImageElement) {
    const canvas = document.createElement("canvas");
    canvas.width = img.width;
    canvas.height = img.height;
    const ctx = canvas.getContext("2d", {alpha: false});
    if (!ctx) {
      return '';
    }

    ctx.drawImage(img, 0, 0);
    return canvas.toDataURL("image/png");
  }

  selectImage(index: number, callback?: Function) {
    if (this.selectedIndex === index) { return; }

    // If we load custom images of series/chapters/covers, then those urls are not properly encoded, so on select we have to clean them up
    if (!this.imageUrls[index].startsWith('data:image/')) {
      const imgUrl = this.imageUrls[index];
      const img = new Image();
      img.crossOrigin = 'Anonymous';
      img.src = imgUrl;
      img.onload = (e) => {
        this.handleUrlImageAdd(img, index);
        this.selectedBase64Url.emit(this.imageUrls[this.selectedIndex]);
        if (callback) callback(index);
      };
      img.onerror = (e) => {
        this.toastr.error(translate('errors.rejected-cover-upload'));
        this.form.get('coverImageUrl')?.setValue('');
        this.cdRef.markForCheck();
      };
      this.form.get('coverImageUrl')?.setValue('');
      this.cdRef.markForCheck();
      return;
    }

    this.selectedIndex = index;
    this.cdRef.markForCheck();
    this.imageSelected.emit(this.selectedIndex);
    this.selectedBase64Url.emit(this.imageUrls[this.selectedIndex]);
  }

  applyImage(index: number) {
    if (!this.showApplyButton) return;

    this.selectImage(index, () => {
      this.applyCover.emit(this.imageUrls[index]);
      this.appliedIndex = index;
      this.cdRef.markForCheck();
    });
  }

  resetImage() {
    if (this.showApplyButton) {
      this.resetCover.emit();
    }
  }

  loadImage(url?: string) {
    url = url || this.form.get('coverImageUrl')?.value.trim();
    if (!url || url === '') return;

    this.uploadService.uploadByUrl(url).subscribe(filename => {
      const img = new Image();
      img.crossOrigin = 'Anonymous';
      img.src = this.imageService.getCoverUploadImage(filename);
      img.onload = (e) => this.handleUrlImageAdd(img);
      img.onerror = (e) => {
        this.toastr.error(translate('errors.rejected-cover-upload'));
        this.form.get('coverImageUrl')?.setValue('');
        this.cdRef.markForCheck();
      };
      this.form.get('coverImageUrl')?.setValue('');
      this.cdRef.markForCheck();
    });
  }

  loadImageFromUrl(url?: string) {
    url = url || this.form.get('coverImageUrl')?.value.trim();
    if (!url || url === '') return;

    this.uploadService.uploadByUrl(url).subscribe(filename => {
      const img = new Image();
      img.crossOrigin = 'Anonymous';
      img.src = this.imageService.getCoverUploadImage(filename);
      img.onload = (e) => this.handleUrlImageAdd(img);
      img.onerror = (e) => {
        this.toastr.error(translate('errors.rejected-cover-upload'));
        this.form.get('coverImageUrl')?.setValue('');
        this.cdRef.markForCheck();
      };
      this.form.get('coverImageUrl')?.setValue('');
      this.cdRef.markForCheck();
    });
  }



  changeMode(mode: 'url') {
    this.mode = mode;
    this.setupEnterHandler();
    this.cdRef.markForCheck();

    setTimeout(() => (this.document.querySelector('#load-image') as HTMLInputElement)?.focus(), 10);
  }

  public dropped(files: NgxFileDropEntry[]) {
    this.files = files;
    for (const droppedFile of files) {

      // Is it a file?
      if (droppedFile.fileEntry.isFile) {
        const fileEntry = droppedFile.fileEntry as FileSystemFileEntry;
        fileEntry.file((file: File) => {
          const reader  = new FileReader();
          reader.onload = (e) => this.handleFileImageAdd(e);
          reader.readAsDataURL(file);
        });
      }
    }
  }

  handleFileImageAdd(e: any) {
    if (e.target == null) return;

    this.imageUrls.push(e.target.result); // This is base64 already
    this.imageUrlsChange.emit(this.imageUrls);
    this.selectedIndex = this.imageUrls.length - 1;
    this.imageSelected.emit(this.selectedIndex); // Auto select newly uploaded image
    this.selectedBase64Url.emit(e.target.result);
    setTimeout(() => {
      (this.document.querySelector('div.image-card[aria-label="Image ' + this.selectedIndex + '"]') as HTMLElement).focus();
    })
    this.cdRef.markForCheck();
  }

  handleUrlImageAdd(img: HTMLImageElement, index: number = -1) {
    const url = this.getBase64Image(img);
    if (index >= 0) {
      this.imageUrls[index] = url;
    } else {
      this.imageUrls.push(url);
    }

    this.imageUrlsChange.emit(this.imageUrls);
    this.cdRef.markForCheck();

    setTimeout(() => {
      // Auto select newly uploaded image and tell parent of new base64 url
      this.selectImage(index >= 0 ? index : this.imageUrls.length - 1);
    });
  }

  reset() {
    this.resetClicked.emit();
    this.selectedIndex = -1;
  }

  setupEnterHandler() {
    setTimeout(() => {
      const elem = document.querySelector('input[id="load-image"]');
      if (elem == null) return;
      fromEvent(elem, 'keydown')
        .pipe(takeWhile(() => this.mode === 'url')).subscribe((event) => {
          const evt = <KeyboardEvent>event;
          switch(evt.key) {
            case KEY_CODES.ENTER:
            {
              this.loadImage();
              break;
            }

            case KEY_CODES.ESC_KEY:
              this.mode = 'all';
              event.stopPropagation();
              break;
            default:
              break;
          }
        });
    });
  }

  protected fileOver(event: any){}
  protected fileLeave(event: any){}
}
