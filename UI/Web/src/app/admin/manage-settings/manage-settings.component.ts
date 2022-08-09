import { Component, OnInit } from '@angular/core';
import { UntypedFormGroup, UntypedFormControl, Validators } from '@angular/forms';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { SettingsService } from '../settings.service';
import { DirectoryPickerComponent, DirectoryPickerResult } from '../_modals/directory-picker/directory-picker.component';
import { ServerSettings } from '../_models/server-settings';


@Component({
  selector: 'app-manage-settings',
  templateUrl: './manage-settings.component.html',
  styleUrls: ['./manage-settings.component.scss']
})
export class ManageSettingsComponent implements OnInit {

  serverSettings!: ServerSettings;
  settingsForm: UntypedFormGroup = new UntypedFormGroup({});
  taskFrequencies: Array<string> = [];
  logLevels: Array<string> = [];

  constructor(private settingsService: SettingsService, private toastr: ToastrService, 
    private modalService: NgbModal) { }

  ngOnInit(): void {
    this.settingsService.getTaskFrequencies().pipe(take(1)).subscribe(frequencies => {
      this.taskFrequencies = frequencies;
    });
    this.settingsService.getLoggingLevels().pipe(take(1)).subscribe(levels => {
      this.logLevels = levels;
    });
    this.settingsService.getServerSettings().pipe(take(1)).subscribe((settings: ServerSettings) => {
      this.serverSettings = settings;
      this.settingsForm.addControl('cacheDirectory', new UntypedFormControl(this.serverSettings.cacheDirectory, [Validators.required]));
      this.settingsForm.addControl('bookmarksDirectory', new UntypedFormControl(this.serverSettings.bookmarksDirectory, [Validators.required]));
      this.settingsForm.addControl('taskScan', new UntypedFormControl(this.serverSettings.taskScan, [Validators.required]));
      this.settingsForm.addControl('taskBackup', new UntypedFormControl(this.serverSettings.taskBackup, [Validators.required]));
      this.settingsForm.addControl('port', new UntypedFormControl(this.serverSettings.port, [Validators.required]));
      this.settingsForm.addControl('loggingLevel', new UntypedFormControl(this.serverSettings.loggingLevel, [Validators.required]));
      this.settingsForm.addControl('allowStatCollection', new UntypedFormControl(this.serverSettings.allowStatCollection, [Validators.required]));
      this.settingsForm.addControl('enableOpds', new UntypedFormControl(this.serverSettings.enableOpds, [Validators.required]));
      this.settingsForm.addControl('baseUrl', new UntypedFormControl(this.serverSettings.baseUrl, [Validators.required]));
      this.settingsForm.addControl('emailServiceUrl', new UntypedFormControl(this.serverSettings.emailServiceUrl, [Validators.required]));
      this.settingsForm.addControl('enableSwaggerUi', new UntypedFormControl(this.serverSettings.enableSwaggerUi, [Validators.required]));
      this.settingsForm.addControl('totalBackups', new UntypedFormControl(this.serverSettings.totalBackups, [Validators.required, Validators.min(1), Validators.max(30)]));
    });
  }

  resetForm() {
    this.settingsForm.get('cacheDirectory')?.setValue(this.serverSettings.cacheDirectory);
    this.settingsForm.get('bookmarksDirectory')?.setValue(this.serverSettings.bookmarksDirectory);
    this.settingsForm.get('scanTask')?.setValue(this.serverSettings.taskScan);
    this.settingsForm.get('taskBackup')?.setValue(this.serverSettings.taskBackup);
    this.settingsForm.get('port')?.setValue(this.serverSettings.port);
    this.settingsForm.get('loggingLevel')?.setValue(this.serverSettings.loggingLevel);
    this.settingsForm.get('allowStatCollection')?.setValue(this.serverSettings.allowStatCollection);
    this.settingsForm.get('enableOpds')?.setValue(this.serverSettings.enableOpds);
    this.settingsForm.get('baseUrl')?.setValue(this.serverSettings.baseUrl);
    this.settingsForm.get('emailServiceUrl')?.setValue(this.serverSettings.emailServiceUrl);
    this.settingsForm.get('enableSwaggerUi')?.setValue(this.serverSettings.enableSwaggerUi);
    this.settingsForm.get('totalBackups')?.setValue(this.serverSettings.totalBackups);
  }

  async saveSettings() {
    const modelSettings = this.settingsForm.value;

    this.settingsService.updateServerSettings(modelSettings).pipe(take(1)).subscribe(async (settings: ServerSettings) => {
      this.serverSettings = settings;
      this.resetForm();
      this.toastr.success('Server settings updated');
    }, (err: any) => {
      console.error('error: ', err);
    });
  }

  resetToDefaults() {
    this.settingsService.resetServerSettings().pipe(take(1)).subscribe(async (settings: ServerSettings) => {
      this.serverSettings = settings;
      this.resetForm();
      this.toastr.success('Server settings updated');
    }, (err: any) => {
      console.error('error: ', err);
    });
  }

  openDirectoryChooser(existingDirectory: string, formControl: string) {
    const modalRef = this.modalService.open(DirectoryPickerComponent, { scrollable: true, size: 'lg' });
    modalRef.componentInstance.startingFolder = existingDirectory || '';
    modalRef.componentInstance.helpUrl = '';
    modalRef.closed.subscribe((closeResult: DirectoryPickerResult) => {
      if (closeResult.success) {
        this.settingsForm.get(formControl)?.setValue(closeResult.folderPath);
        this.settingsForm.markAsTouched();
      }
    });
  }
}
