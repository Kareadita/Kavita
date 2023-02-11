import { Component, OnInit } from '@angular/core';
import { FormGroup, Validators, FormControl } from '@angular/forms';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { TagBadgeCursor } from 'src/app/shared/tag-badge/tag-badge.component';
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
  settingsForm: FormGroup = new FormGroup({});
  taskFrequencies: Array<string> = [];
  logLevels: Array<string> = [];

  get TagBadgeCursor() {
    return TagBadgeCursor;
  }

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
      this.settingsForm.addControl('cacheDirectory', new FormControl(this.serverSettings.cacheDirectory, [Validators.required]));
      this.settingsForm.addControl('bookmarksDirectory', new FormControl(this.serverSettings.bookmarksDirectory, [Validators.required]));
      this.settingsForm.addControl('taskScan', new FormControl(this.serverSettings.taskScan, [Validators.required]));
      this.settingsForm.addControl('taskBackup', new FormControl(this.serverSettings.taskBackup, [Validators.required]));
      this.settingsForm.addControl('ipAddresses', new FormControl(this.serverSettings.ipAddresses, [Validators.required, Validators.pattern(/^(\s*((([12]?\d{1,2}\.){3}[12]?\d{1,2})|(([\da-f]{0,4}\:){0,7}([\da-f]{0,4})))\s*\,)*\s*((([12]?\d{1,2}\.){3}[12]?\d{1,2})|(([\da-f]{0,4}\:){0,7}([\da-f]{0,4})))\s*$/i)]));
      this.settingsForm.addControl('port', new FormControl(this.serverSettings.port, [Validators.required]));
      this.settingsForm.addControl('loggingLevel', new FormControl(this.serverSettings.loggingLevel, [Validators.required]));
      this.settingsForm.addControl('allowStatCollection', new FormControl(this.serverSettings.allowStatCollection, [Validators.required]));
      this.settingsForm.addControl('enableOpds', new FormControl(this.serverSettings.enableOpds, [Validators.required]));
      this.settingsForm.addControl('baseUrl', new FormControl(this.serverSettings.baseUrl, [Validators.required]));
      this.settingsForm.addControl('emailServiceUrl', new FormControl(this.serverSettings.emailServiceUrl, [Validators.required]));
      this.settingsForm.addControl('totalBackups', new FormControl(this.serverSettings.totalBackups, [Validators.required, Validators.min(1), Validators.max(30)]));
      this.settingsForm.addControl('totalLogs', new FormControl(this.serverSettings.totalLogs, [Validators.required, Validators.min(1), Validators.max(30)]));
      this.settingsForm.addControl('enableFolderWatching', new FormControl(this.serverSettings.enableFolderWatching, [Validators.required]));
      this.settingsForm.addControl('convertBookmarkToWebP', new FormControl(this.serverSettings.convertBookmarkToWebP, []));
      this.settingsForm.addControl('hostName', new FormControl(this.serverSettings.hostName, [Validators.pattern(/^(http:|https:)+[^\s]+[\w]$/)]));
    });
  }

  resetForm() {
    this.settingsForm.get('cacheDirectory')?.setValue(this.serverSettings.cacheDirectory);
    this.settingsForm.get('bookmarksDirectory')?.setValue(this.serverSettings.bookmarksDirectory);
    this.settingsForm.get('scanTask')?.setValue(this.serverSettings.taskScan);
    this.settingsForm.get('taskBackup')?.setValue(this.serverSettings.taskBackup);
    this.settingsForm.get('ipAddresses')?.setValue(this.serverSettings.ipAddresses);
    this.settingsForm.get('port')?.setValue(this.serverSettings.port);
    this.settingsForm.get('loggingLevel')?.setValue(this.serverSettings.loggingLevel);
    this.settingsForm.get('allowStatCollection')?.setValue(this.serverSettings.allowStatCollection);
    this.settingsForm.get('enableOpds')?.setValue(this.serverSettings.enableOpds);
    this.settingsForm.get('baseUrl')?.setValue(this.serverSettings.baseUrl);
    this.settingsForm.get('emailServiceUrl')?.setValue(this.serverSettings.emailServiceUrl);
    this.settingsForm.get('totalBackups')?.setValue(this.serverSettings.totalBackups);
    this.settingsForm.get('totalLogs')?.setValue(this.serverSettings.totalLogs);
    this.settingsForm.get('enableFolderWatching')?.setValue(this.serverSettings.enableFolderWatching);
    this.settingsForm.get('convertBookmarkToWebP')?.setValue(this.serverSettings.convertBookmarkToWebP);
    this.settingsForm.get('hostName')?.setValue(this.serverSettings.hostName);
    this.settingsForm.markAsPristine();
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
      if (closeResult.success && closeResult.folderPath !== '') {
        this.settingsForm.get(formControl)?.setValue(closeResult.folderPath);
        this.settingsForm.markAsDirty();
      }
    });
  }
}
