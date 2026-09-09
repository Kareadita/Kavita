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
import {SettingEnumSelectComponent} from "../../settings/_components/setting-enum-select/setting-enum-select.component";

interface FormModel {
  encodeMediaAs: EncodeFormat;
  bookmarksDirectory: string;
  coverImageSize: CoverImageSize;
  pdfRenderResolution: PdfRenderResolution;
}


@Component({
  selector: 'app-manage-media-settings',
  templateUrl: './manage-media-settings.component.html',
  styleUrls: ['./manage-media-settings.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, SettingItemComponent, EncodeFormatPipe, CoverImageSizePipe, PdfRenderResolutionPipe, FormFieldDirective, FormField, SettingEnumSelectComponent]
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
    encodeMediaAs: EncodeFormat.PNG,
    bookmarksDirectory: '',
    coverImageSize: CoverImageSize.Default,
    pdfRenderResolution: PdfRenderResolution.Default
  });
  protected readonly settingsForm = form(this.formModel, p => {
    required(p.encodeMediaAs);
    readonly(p.bookmarksDirectory);
    required(p.coverImageSize);
    required(p.pdfRenderResolution);
  });

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
      encodeMediaAs: settings.encodeMediaAs,
      bookmarksDirectory: settings.bookmarksDirectory,
      coverImageSize: (settings.coverImageSize || CoverImageSize.Default),
      pdfRenderResolution: (settings.pdfRenderResolution || PdfRenderResolution.Default),
    });
    this.settingsForm().reset();
  }

  packData() {
    const model = this.formModel();
    const modelSettings = Object.assign({}, this.serverSettings);
    modelSettings.encodeMediaAs = model.encodeMediaAs;
    modelSettings.bookmarksDirectory = model.bookmarksDirectory;
    modelSettings.coverImageSize = model.coverImageSize;
    modelSettings.pdfRenderResolution = model.pdfRenderResolution;

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
