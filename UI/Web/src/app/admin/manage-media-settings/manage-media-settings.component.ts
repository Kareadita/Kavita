import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {ToastrService} from 'ngx-toastr';
import {debounceTime, distinctUntilChanged, filter, switchMap, take, tap} from 'rxjs';
import {SettingsService} from '../settings.service';
import {ServerSettings} from '../_models/server-settings';
import {DirectoryPickerComponent, DirectoryPickerResult} from '../_modals/directory-picker/directory-picker.component';
import {
  NgbAccordionBody,
  NgbAccordionButton,
  NgbAccordionCollapse,
  NgbAccordionDirective,
  NgbAccordionHeader,
  NgbAccordionItem,
  NgbAccordionToggle,
  NgbCollapse,
  NgbModal,
  NgbTooltip
} from '@ng-bootstrap/ng-bootstrap';
import {allEncodeFormats} from '../_models/encode-format';
import {ManageMediaIssuesComponent} from '../manage-media-issues/manage-media-issues.component';
import {NgFor, NgIf, NgTemplateOutlet} from '@angular/common';
import {translate, TranslocoDirective, TranslocoService} from "@jsverse/transloco";
import {allCoverImageSizes, CoverImageSize} from '../_models/cover-image-size';
import {pageLayoutModes} from "../../_models/preferences/preferences";
import {PageLayoutModePipe} from "../../_pipes/page-layout-mode.pipe";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {EncodeFormatPipe} from "../../_pipes/encode-format.pipe";
import {CoverImageSizePipe} from "../../_pipes/cover-image-size.pipe";
import {ConfirmService} from "../../shared/confirm.service";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

@Component({
  selector: 'app-manage-media-settings',
  templateUrl: './manage-media-settings.component.html',
  styleUrls: ['./manage-media-settings.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [NgIf, ReactiveFormsModule, NgbTooltip, NgTemplateOutlet, NgFor, NgbAccordionDirective, NgbAccordionItem,
    NgbAccordionHeader, NgbAccordionToggle, NgbAccordionButton, NgbCollapse, NgbAccordionCollapse, NgbAccordionBody,
    ManageMediaIssuesComponent, TranslocoDirective, PageLayoutModePipe, SettingItemComponent, EncodeFormatPipe, CoverImageSizePipe]
})
export class ManageMediaSettingsComponent implements OnInit {

  private readonly translocoService = inject(TranslocoService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly confirmService = inject(ConfirmService);
  private readonly settingsService = inject(SettingsService);
  private readonly toastr = inject(ToastrService);
  private readonly modalService = inject(NgbModal);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly allEncodeFormats = allEncodeFormats;
  protected readonly allCoverImageSizes = allCoverImageSizes;

  serverSettings!: ServerSettings;
  settingsForm: FormGroup = new FormGroup({});


  ngOnInit(): void {
    this.settingsService.getServerSettings().pipe(take(1)).subscribe((settings: ServerSettings) => {
      this.serverSettings = settings;
      this.settingsForm.addControl('encodeMediaAs', new FormControl(this.serverSettings.encodeMediaAs, [Validators.required]));
      this.settingsForm.addControl('bookmarksDirectory', new FormControl(this.serverSettings.bookmarksDirectory, [Validators.required]));
      this.settingsForm.addControl('coverImageSize', new FormControl(this.serverSettings.coverImageSize || CoverImageSize.Default, [Validators.required]));

      // Automatically save settings as we edit them
      this.settingsForm.valueChanges.pipe(
        distinctUntilChanged(),
        debounceTime(100),
        filter(_ => this.settingsForm.valid),
        takeUntilDestroyed(this.destroyRef),
        switchMap(_ => {
          const data = this.packData();
          return this.settingsService.updateServerSettings(data);
        }),
        tap(settings => {

          const encodingChanged = this.serverSettings.encodeMediaAs !== settings.encodeMediaAs;
          if (encodingChanged) {
            this.toastr.info(translate('manage-media-settings.media-warning'));
          }

          this.serverSettings = settings;
          this.resetForm();
          this.cdRef.markForCheck();
        })
      ).subscribe();

      this.cdRef.markForCheck();
    });
  }

  resetForm() {
    this.settingsForm.get('encodeMediaAs')?.setValue(this.serverSettings.encodeMediaAs, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('bookmarksDirectory')?.setValue(this.serverSettings.bookmarksDirectory, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('coverImageSize')?.setValue(this.serverSettings.coverImageSize, {onlySelf: true, emitEvent: false});
    this.settingsForm.markAsPristine();
    this.cdRef.markForCheck();
  }

  packData() {
    const modelSettings = Object.assign({}, this.serverSettings);
    modelSettings.encodeMediaAs = parseInt(this.settingsForm.get('encodeMediaAs')?.value, 10);
    modelSettings.bookmarksDirectory = this.settingsForm.get('bookmarksDirectory')?.value;
    modelSettings.coverImageSize = parseInt(this.settingsForm.get('coverImageSize')?.value, 10);

    return modelSettings;
  }


  async resetToDefaults() {
    if (!await this.confirmService.confirm(translate('toasts.confirm-reset-server-settings'))) return;

    this.settingsService.resetServerSettings().pipe(take(1)).subscribe((settings: ServerSettings) => {
      this.serverSettings = settings;
      this.resetForm();
      this.toastr.success(this.translocoService.translate('toasts.server-settings-updated'));
    }, (err: any) => {
      console.error('error: ', err);
    });
  }

  openDirectoryChooser(existingDirectory: string, formControl: string) {
    const modalRef = this.modalService.open(DirectoryPickerComponent, { scrollable: true, size: 'lg', fullscreen: 'md' });
    modalRef.componentInstance.startingFolder = existingDirectory || '';
    modalRef.componentInstance.helpUrl = '';
    modalRef.closed.subscribe((closeResult: DirectoryPickerResult) => {
      if (closeResult.success && closeResult.folderPath !== '') {
        this.settingsForm.get(formControl)?.setValue(closeResult.folderPath);
        this.settingsForm.markAsDirty();
        this.cdRef.markForCheck();
      }
    });
  }

  protected readonly pageLayoutModes = pageLayoutModes;
}
