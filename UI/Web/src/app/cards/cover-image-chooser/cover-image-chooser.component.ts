import {ChangeDetectionStrategy, Component, effect, inject, input, output, signal} from '@angular/core';
import {FileSystemFileEntry, NgxFileDropEntry} from 'ngx-file-drop';
import {ToastrService} from 'ngx-toastr';
import {ImageService} from 'src/app/_services/image.service';
import {UploadService} from 'src/app/_services/upload.service';
import {ImageComponent} from "../../shared/image/image.component";
import {translate, TranslocoModule} from "@jsverse/transloco";
import {
  FileDragAndDropUploadComponent
} from "src/app/shared/file-drag-and-drop-upload/file-drag-and-drop-upload.component";
import {NgbNav, NgbNavContent, NgbNavItem, NgbNavLink, NgbNavOutlet} from "@ng-bootstrap/ng-bootstrap";
import {TabTitlePipe} from "../../_pipes/tab-title.pipe";
import {Tabs} from "../../_models/tabs";
import {CoverImageChooserConfig, CoverImageOption} from "../../_services/cover-chooser-config-factory.service";
import {NgTemplateOutlet} from "@angular/common";


@Component({
  selector: 'app-cover-image-chooser',
  imports: [
    ImageComponent,
    TranslocoModule,
    FileDragAndDropUploadComponent,
    NgbNav,
    NgbNavItem,
    NgbNavLink,
    NgbNavContent,
    NgbNavOutlet,
    TabTitlePipe,
    NgTemplateOutlet
  ],
  templateUrl: './cover-image-chooser.component.html',
  styleUrls: ['./cover-image-chooser.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CoverImageChooserComponent  {

  public readonly imageService = inject(ImageService);
  private readonly toastr = inject(ToastrService);
  private readonly uploadService = inject(UploadService);

  config = input<CoverImageChooserConfig>({});

  coverChanged = output<{ isDirty: boolean; fileName: string }>();
  resetClicked = output();

  protected readonly acceptableExtensions = ['.png', '.jpg', '.jpeg', '.gif', '.webp', '.avif'].join(',');

  protected readonly volumeImages = signal<CoverImageOption[] | null>(null);
  protected readonly chapterImages = signal<CoverImageOption[] | null>(null);
  protected readonly uploadedImages = signal<CoverImageOption[]>([]);
  protected readonly kavitaplusImages = signal<CoverImageOption[]>([]);
  protected readonly kavitaplusImagesLoaded = signal(false);
  protected readonly otherImages = signal<CoverImageOption[]>([]);
  protected readonly selectedOptionKey = signal<string | null>(null);

  private volumeLoaded = false;
  private chapterLoaded = false;
  private kavitaPlusLoaded = false;
  private otherLoaded = false;
  private hasInit = false;

  activeTabId = Tabs.Current;


  constructor() {
    effect(() => {
      // Keep track of the default tab
      const hasUploadedImages = this.uploadedImages().length > 0;
      const hasSelected = this.config().selected;
      const hasVolume = (this.volumeImages() ?? []).length > 0;
      const hasChapter = (this.chapterImages() ?? []).length > 0;

      if (Object.keys(this.config()).length === 0) return;

      if (this.hasInit) return;

      if (hasSelected) {
        this.activeTabId = Tabs.Current;
      } else if (hasUploadedImages) {
        this.activeTabId = Tabs.Uploaded;
      } else if (hasVolume) {
        this.activeTabId = Tabs.Volumes;
      } else if (hasChapter) {
        this.activeTabId = Tabs.Chapters;
      } else {
        this.activeTabId = Tabs.Uploaded;
      }

      this.hasInit = true;
    });
  }


  selectOption(option: CoverImageOption, sourceTab: Tabs) {
    this.selectedOptionKey.set(option.url);
    const isDirty = sourceTab !== Tabs.Current;

    if (!isDirty) {
      // Selecting the existing cover - nothing to stage or upload.
      this.coverChanged.emit({ isDirty, fileName: '' });
      return;
    }

    // Already staged in temp (Uploaded tab) - emit its filename directly.
    if (option.fileName) {
      this.coverChanged.emit({ isDirty, fileName: option.fileName });
      return;
    }

    // Remote option (Volumes/Chapters/Kavita+/Other): stage it into temp, then emit the resulting filename.
    this.uploadService.uploadByUrl(option.url, true).subscribe({
      next: filename => {
        option.fileName = filename;
        this.coverChanged.emit({ isDirty, fileName: filename });
      },
      error: () => this.toastr.error(translate('errors.rejected-cover-upload'))
    });
  }

  onTabActivate(tabId: Tabs) {
    if (tabId === Tabs.Volumes && !this.volumeLoaded && this.config().volumeFunc) {
      this.volumeLoaded = true;
      this.config().volumeFunc!.subscribe(items => {this.volumeImages.set(items)});
    } else if (tabId === Tabs.Chapters && !this.chapterLoaded && this.config().chapterFunc) {
      this.chapterLoaded = true;
      this.config().chapterFunc!.subscribe(items => {this.chapterImages.set(items)});
    } else if (tabId === Tabs.KavitaPlus && !this.kavitaPlusLoaded && this.config().kavitaplusFunc) {
      this.kavitaPlusLoaded = true;
      this.config().kavitaplusFunc!.subscribe(items => {
        this.kavitaplusImages.set(items);
        this.kavitaplusImagesLoaded.set(true);
      });
    } else if (tabId === Tabs.Other && !this.otherLoaded && this.config().otherFunc) {
      this.otherLoaded = true;
      this.config().otherFunc!.subscribe(items => {this.otherImages.set(items)});
    }
  }

  reset() {
    const fn = this.config().resetFunc;
    if (fn) {
      fn().subscribe(() => {
        this.resetClicked.emit();
        this.selectedOptionKey.set(null);
      });
    } else {
      this.resetClicked.emit();
      this.selectedOptionKey.set(null);
    }
  }

  public dropped(files: NgxFileDropEntry[]) {
    for (const droppedFile of files) {
      if (droppedFile.fileEntry.isFile) {
        const fileEntry = droppedFile.fileEntry as FileSystemFileEntry;
        fileEntry.file((file: File) => {
          this.uploadService.uploadByFile(file).subscribe({
            next: filename => this.addStagedImage(filename),
            error: () => this.toastr.error(translate('errors.rejected-cover-upload'))
          });
        });
      }
    }
  }

  loadImage(url: string) {
    if (!url || url === '') return;

    this.uploadService.uploadByUrl(url).subscribe({
      next: filename => this.addStagedImage(filename),
      error: () => this.toastr.error(translate('errors.rejected-cover-upload'))
    });
  }

  /**
   * Adds an image that has already been staged in the temp directory to the Uploaded tab and selects it.
   */
  private addStagedImage(filename: string) {
    const option: CoverImageOption = {
      url: this.imageService.getCoverUploadImage(filename),
      title: '',
      fileName: filename
    };
    this.uploadedImages.update(imgs => [...imgs, option]);
    this.selectOption(option, Tabs.Uploaded);
  }

  addImage(option: CoverImageOption) {
    this.uploadedImages.update(imgs => [...imgs, option]);
    this.selectOption(option, Tabs.Uploaded);
  }

  protected readonly Tabs = Tabs;
}
