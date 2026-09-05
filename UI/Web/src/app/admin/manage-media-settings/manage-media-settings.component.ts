import {ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal} from '@angular/core';
import {ToastrService} from '@openng/ngx-toastr';
import {catchError, debounceTime, distinctUntilChanged, filter, of, switchMap, tap} from 'rxjs';
import {SettingsService} from '../settings.service';
import {ServerSettings} from '../_models/server-settings';
import {
  DirectoryPickerModalComponent,
  DirectoryPickerResult
} from '../_modals/directory-picker/directory-picker-modal.component';
import {allEncodeFormats, EncodeFormat} from '../_models/encode-format';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {allCoverImageSizes, CoverImageSize} from '../_models/cover-image-size';
import {allPdfRenderResolutions, PdfRenderResolution} from '../_models/pdf-render-resolution';
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {EncodeFormatPipe} from "../../_pipes/encode-format.pipe";
import {CoverImageSizePipe} from "../../_pipes/cover-image-size.pipe";
import {PdfRenderResolutionPipe} from "../../_pipes/pdf-render-resolution.pipe"
import {takeUntilDestroyed, toObservable} from "@angular/core/rxjs-interop";
import {ModalService} from "../../_services/modal.service";
import {FormFieldDirective} from "../../_directives/form-field.directive";
import {form, FormField, readonly, required} from "@angular/forms/signals";

interface FormModel {
  encodeMediaAs: string;
  bookmarksDirectory: string;
  coverImageSize: string;
  pdfRenderResolution: string;
}


@Component({
  selector: 'app-manage-media-settings',
  templateUrl: './manage-media-settings.component.html',
  styleUrls: ['./manage-media-settings.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, SettingItemComponent, EncodeFormatPipe, CoverImageSizePipe, PdfRenderResolutionPipe, FormFieldDirective, FormField]
})
export class ManageMediaSettingsComponent implements OnInit {

  private readonly settingsService = inject(SettingsService);
  private readonly toastr = inject(ToastrService);
  private readonly modalService = inject(ModalService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly allEncodeFormats = allEncodeFormats;
  protected readonly allCoverImageSizes = allCoverImageSizes;
  protected readonly allPdfRenderResolutions = allPdfRenderResolutions;

  private serverSettings!: ServerSettings;
  protected readonly isLoaded = signal(false);

  private readonly formModel = signal<FormModel>({
    encodeMediaAs: '',
    bookmarksDirectory: '',
    coverImageSize: '',
    pdfRenderResolution: ''
  });
  protected readonly settingsForm = form(this.formModel, p => {
    required(p.encodeMediaAs);
    readonly(p.bookmarksDirectory);
    required(p.coverImageSize);
    required(p.pdfRenderResolution);
  });

  protected readonly encodeMediaAs = computed(() => parseInt(this.formModel().encodeMediaAs, 10) as EncodeFormat);
  protected readonly coverImageSize = computed(() => parseInt(this.formModel().coverImageSize, 10) as CoverImageSize);
  protected readonly pdfRenderResolution = computed(() => parseInt(this.formModel().pdfRenderResolution, 10) as PdfRenderResolution);

  constructor() {
    // Automatically save settings as we edit them
    toObservable(this.formModel).pipe(
      debounceTime(100),
      distinctUntilChanged((a, b) => a.encodeMediaAs === b.encodeMediaAs
        && a.bookmarksDirectory === b.bookmarksDirectory
        && a.coverImageSize === b.coverImageSize
        && a.pdfRenderResolution === b.pdfRenderResolution),
      filter(() => this.settingsForm().dirty() && this.settingsForm().valid()),
      switchMap(() => this.settingsService.updateServerSettings(this.packData()).pipe(catchError(err => {
        console.error(err);
        return of(null);
      }))),
      tap(settings => {
        if (!settings) {
          return;
        }

        const encodingChanged = this.serverSettings.encodeMediaAs !== settings.encodeMediaAs;
        if (encodingChanged) {
          this.toastr.info(translate('manage-media-settings.media-warning'));
        }

        if ('result' in settings && 'value' in settings) {
          this.serverSettings = (settings as any).value;
        } else {
          this.serverSettings = settings;
        }

        this.resetForm();
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe();
  }

  ngOnInit(): void {
    this.settingsService.getServerSettings().subscribe((settings: ServerSettings) => {
      this.serverSettings = settings;
      this.resetForm();
      this.isLoaded.set(true);
    });
  }

  resetForm() {
    const settings = this.serverSettings;
    this.formModel.set({
      encodeMediaAs: settings.encodeMediaAs.toString(),
      bookmarksDirectory: settings.bookmarksDirectory,
      coverImageSize: (settings.coverImageSize || CoverImageSize.Default).toString(),
      pdfRenderResolution: (settings.pdfRenderResolution || PdfRenderResolution.Default).toString(),
    });
    this.settingsForm().reset();
  }

  packData() {
    const model = this.formModel();
    const modelSettings = Object.assign({}, this.serverSettings);
    modelSettings.encodeMediaAs = parseInt(model.encodeMediaAs, 10);
    modelSettings.bookmarksDirectory = model.bookmarksDirectory;
    modelSettings.coverImageSize = parseInt(model.coverImageSize, 10);
    modelSettings.pdfRenderResolution = parseInt(model.pdfRenderResolution, 10);

    return modelSettings;
  }


  openDirectoryChooser(existingDirectory: string) {
    const modalRef = this.modalService.open(DirectoryPickerModalComponent);
    modalRef.setInput('startingFolder', existingDirectory || '');
    modalRef.setInput('helpUrl', '');

    modalRef.closed.subscribe((closeResult: DirectoryPickerResult) => {
      if (closeResult.success && closeResult.folderPath !== '') {
        this.settingsForm.bookmarksDirectory().value.set(closeResult.folderPath);
        this.settingsForm.bookmarksDirectory().markAsDirty();
      }
    });
  }
}
