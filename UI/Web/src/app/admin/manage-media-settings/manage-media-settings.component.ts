import {Component, inject, OnInit} from '@angular/core';
import { FormGroup, FormControl, Validators, ReactiveFormsModule } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs';
import { SettingsService } from '../settings.service';
import { ServerSettings } from '../_models/server-settings';
import { DirectoryPickerComponent, DirectoryPickerResult } from '../_modals/directory-picker/directory-picker.component';
import { NgbModal, NgbTooltip, NgbAccordionDirective, NgbAccordionItem, NgbAccordionHeader, NgbAccordionToggle, NgbAccordionButton, NgbCollapse, NgbAccordionCollapse, NgbAccordionBody } from '@ng-bootstrap/ng-bootstrap';
import { EncodeFormats } from '../_models/encode-format';
import { ManageScrobbleErrorsComponent } from '../manage-scrobble-errors/manage-scrobble-errors.component';
import { ManageAlertsComponent } from '../manage-alerts/manage-alerts.component';
import { NgIf, NgTemplateOutlet, NgFor } from '@angular/common';
import {TranslocoModule, TranslocoService} from "@ngneat/transloco";

@Component({
    selector: 'app-manage-media-settings',
    templateUrl: './manage-media-settings.component.html',
    styleUrls: ['./manage-media-settings.component.scss'],
    standalone: true,
  imports: [NgIf, ReactiveFormsModule, NgbTooltip, NgTemplateOutlet, NgFor, NgbAccordionDirective, NgbAccordionItem, NgbAccordionHeader, NgbAccordionToggle, NgbAccordionButton, NgbCollapse, NgbAccordionCollapse, NgbAccordionBody, ManageAlertsComponent, ManageScrobbleErrorsComponent, TranslocoModule]
})
export class ManageMediaSettingsComponent implements OnInit {

  serverSettings!: ServerSettings;
  settingsForm: FormGroup = new FormGroup({});

  alertCount: number = 0;
  scrobbleCount: number = 0;

  private readonly translocoService = inject(TranslocoService);

  get EncodeFormats() { return EncodeFormats; }

  constructor(private settingsService: SettingsService, private toastr: ToastrService, private modalService: NgbModal, ) { }

  ngOnInit(): void {
    this.settingsService.getServerSettings().pipe(take(1)).subscribe((settings: ServerSettings) => {
      this.serverSettings = settings;
      this.settingsForm.addControl('encodeMediaAs', new FormControl(this.serverSettings.encodeMediaAs, [Validators.required]));
      this.settingsForm.addControl('bookmarksDirectory', new FormControl(this.serverSettings.bookmarksDirectory, [Validators.required]));
    });
  }

  resetForm() {
    this.settingsForm.get('encodeMediaAs')?.setValue(this.serverSettings.encodeMediaAs);
    this.settingsForm.get('bookmarksDirectory')?.setValue(this.serverSettings.bookmarksDirectory);
    this.settingsForm.markAsPristine();
  }

  saveSettings() {
    const modelSettings = Object.assign({}, this.serverSettings);
    modelSettings.encodeMediaAs = parseInt(this.settingsForm.get('encodeMediaAs')?.value, 10);
    modelSettings.bookmarksDirectory = this.settingsForm.get('bookmarksDirectory')?.value;

    this.settingsService.updateServerSettings(modelSettings).pipe(take(1)).subscribe(async (settings: ServerSettings) => {
      this.serverSettings = settings;
      this.resetForm();
      this.toastr.success(this.translocoService.translate('toasts.server-settings-updated'));
    }, (err: any) => {
      console.error('error: ', err);
    });
  }

  resetToDefaults() {
    this.settingsService.resetServerSettings().pipe(take(1)).subscribe((settings: ServerSettings) => {
      this.serverSettings = settings;
      this.resetForm();
      this.toastr.success(this.translocoService.translate('toasts.server-settings-updated'));
    }, (err: any) => {
      console.error('error: ', err);
    });
  }

  openDirectoryChooser(existingDirectory: string, formControl: string) {
    const modalRef = this.modalService.open(DirectoryPickerComponent, { scrollable: true, size: 'lg' });
    modalRef.componentInstance.startingFolder = existingDirectory || '';
    modalRef.componentInstance.helpUrl = '';
    modalRef.closed.subscribe((closeResult: DirectoryPickerResult) => {
      if (closeResult.success && closeResult.folderPath !== '') {
        this.settingsForm.get(formControl)?.setValue(closeResult.folderPath);
        this.settingsForm.markAsDirty();
      }
    });
  }
}
