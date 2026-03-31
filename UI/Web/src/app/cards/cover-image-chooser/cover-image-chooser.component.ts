import {ChangeDetectionStrategy, Component, inject, input, model, output, signal} from '@angular/core';
import {FileSystemFileEntry, NgxFileDropEntry, NgxFileDropModule} from 'ngx-file-drop';
import {fromEvent} from 'rxjs';
import {takeWhile} from 'rxjs/operators';
import {ToastrService} from 'ngx-toastr';
import {ImageService} from 'src/app/_services/image.service';
import {KEY_CODES} from 'src/app/shared/_services/utility.service';
import {UploadService} from 'src/app/_services/upload.service';
import {DOCUMENT} from '@angular/common';
import {ImageComponent} from "../../shared/image/image.component";
import {translate, TranslocoModule} from "@jsverse/transloco";
import {ColorscapeService} from "../../_services/colorscape.service";
import {
  FileDragAndDropUploadComponent
} from "src/app/shared/file-drag-and-drop-upload/file-drag-and-drop-upload.component";

@Component({
  selector: 'app-cover-image-chooser',
  imports: [
    NgxFileDropModule,
    ImageComponent,
    TranslocoModule,
    FileDragAndDropUploadComponent
  ],
  templateUrl: './cover-image-chooser.component.html',
  styleUrls: ['./cover-image-chooser.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CoverImageChooserComponent {

  public readonly imageService = inject(ImageService);
  private readonly toastr = inject(ToastrService);
  private readonly uploadService = inject(UploadService);
  private readonly colorscapeService = inject(ColorscapeService);
  private readonly document = inject(DOCUMENT);

  /**
   * If buttons show under images to allow immediate selection of cover images.
   */
  showApplyButton = input<boolean>(false);
  imageUrls = model<string[]>([]);
  /**
   * Should the control give the ability to select an image that emits the reset status for cover image
   */
  showReset = input<boolean>(false);
  /**
   * When a cover image is selected, this will be called with a base url representation of the file.
   */
  applyCover = output<string>();
  /**
   * When a cover image is reset, this will be called.
   */
  resetCover = output();
  resetClicked = output();

  /**
   * Emits the selected index. Used usually to check if something other than the default image was selected.
   */
  imageSelected = output<number>();
  /**
   * Emits a base64 encoded image
   */
  selectedBase64Url = output<string>();

  selectedIndex = signal(0);
  /**
   * Only applies for showApplyButton. Used to track which image is applied.
   */
  appliedIndex = signal(0);
  coverImageUrl = signal('');
  acceptableExtensions = ['.png', '.jpg', '.jpeg', '.gif', '.webp', '.avif'].join(',');
  mode = signal<'file' | 'url' | 'all'>('all');

  selectImage(index: number, callback?: (index: number) => void) {
    if (this.selectedIndex() === index) { return; }

    // If we load custom images of series/chapters/covers, then those urls are not properly encoded, so on select we have to clean them up
    if (!this.imageUrls()[index].startsWith('data:image/')) {
      const imgUrl = this.imageUrls()[index];
      const img = new Image();
      img.crossOrigin = 'Anonymous';
      img.src = imgUrl;
      img.onload = () => {
        this.handleUrlImageAdd(img, index);
        this.selectedBase64Url.emit(this.imageUrls()[this.selectedIndex()]);
        if (callback) callback(index);
      };
      img.onerror = () => {
        this.toastr.error(translate('errors.rejected-cover-upload'));
      };
      return;
    }

    this.selectedIndex.set(index);
    this.imageSelected.emit(this.selectedIndex());
    this.selectedBase64Url.emit(this.imageUrls()[this.selectedIndex()]);
  }

  applyImage(index: number) {
    if (!this.showApplyButton()) return;

    this.selectImage(index, () => {
      this.applyCover.emit(this.imageUrls()[index]);
      this.appliedIndex.set(index);
    });
  }

  resetImage() {
    if (this.showApplyButton()) {
      this.resetCover.emit(undefined);
    }
  }

  loadImage(url: string) {
    if (!url || url === '') return;

    this.uploadService.uploadByUrl(url).subscribe(filename => {
      const img = new Image();
      img.crossOrigin = 'Anonymous';
      img.src = this.imageService.getCoverUploadImage(filename);
      img.onload = () => this.handleUrlImageAdd(img);
      img.onerror = () => {
        this.toastr.error(translate('errors.rejected-cover-upload'));
      };
    });
  }

  public dropped(files: NgxFileDropEntry[]) {
    for (const droppedFile of files) {
      if (droppedFile.fileEntry.isFile) {
        const fileEntry = droppedFile.fileEntry as FileSystemFileEntry;
        fileEntry.file((file: File) => {
          const reader = new FileReader();
          reader.onload = (e) => this.handleFileImageAdd(e);
          reader.readAsDataURL(file);
        });
      }
    }
  }

  handleFileImageAdd(e: any) {
    if (e.target == null) return;

    this.imageUrls.update(urls => [...urls, e.target.result]);
    this.selectedIndex.set(this.imageUrls().length - 1);
    this.imageSelected.emit(this.selectedIndex());
    this.selectedBase64Url.emit(e.target.result);
    setTimeout(() => {
      (this.document.querySelector('div.clickable[aria-label="Image ' + (this.selectedIndex() + 1) + '"]') as HTMLElement).focus();
    });
  }

  handleUrlImageAdd(img: HTMLImageElement, index: number = -1) {
    const url = this.colorscapeService.getBase64Image(img);
    if (index >= 0) {
      this.imageUrls.update(urls => urls.map((u, i) => i === index ? url : u));
    } else {
      this.imageUrls.update(urls => [...urls, url]);
    }

    setTimeout(() => {
      this.selectImage(index >= 0 ? index : this.imageUrls().length - 1);
    });
  }

  reset() {
    this.resetClicked.emit(undefined);
    this.selectedIndex.set(-1);
  }
}
