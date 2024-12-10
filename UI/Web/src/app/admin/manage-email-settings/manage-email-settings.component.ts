import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {ToastrService} from 'ngx-toastr';
import {debounceTime, distinctUntilChanged, filter, switchMap, take, tap} from 'rxjs';
import {SettingsService} from '../settings.service';
import {ServerSettings} from '../_models/server-settings';
import {translate, TranslocoModule} from "@jsverse/transloco";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {SettingSwitchComponent} from "../../settings/_components/setting-switch/setting-switch.component";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {BytesPipe} from "../../_pipes/bytes.pipe";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

@Component({
  selector: 'app-manage-email-settings',
  templateUrl: './manage-email-settings.component.html',
  styleUrls: ['./manage-email-settings.component.scss'],
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, TranslocoModule, SettingItemComponent, SettingSwitchComponent, DefaultValuePipe, BytesPipe]
})
export class ManageEmailSettingsComponent implements OnInit {

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly settingsService = inject(SettingsService);
  private readonly toastr = inject(ToastrService);
  private readonly destroyRef = inject(DestroyRef);

  serverSettings!: ServerSettings;
  settingsForm: FormGroup = new FormGroup({});

  ngOnInit(): void {
    this.settingsService.getServerSettings().pipe(take(1)).subscribe((settings: ServerSettings) => {
      this.serverSettings = settings;
      this.settingsForm.addControl('hostName', new FormControl(this.serverSettings.hostName, [Validators.pattern(/^(http:|https:)+[^\s]+[\w]$/)]));

      this.settingsForm.addControl('host', new FormControl(this.serverSettings.smtpConfig.host, []));
      this.settingsForm.addControl('port', new FormControl(this.serverSettings.smtpConfig.port, []));
      this.settingsForm.addControl('userName', new FormControl(this.serverSettings.smtpConfig.userName, []));
      this.settingsForm.addControl('enableSsl', new FormControl(this.serverSettings.smtpConfig.enableSsl, []));
      this.settingsForm.addControl('password', new FormControl(this.serverSettings.smtpConfig.password, []));
      this.settingsForm.addControl('senderAddress', new FormControl(this.serverSettings.smtpConfig.senderAddress, []));
      this.settingsForm.addControl('senderDisplayName', new FormControl(this.serverSettings.smtpConfig.senderDisplayName, []));
      this.settingsForm.addControl('sizeLimit', new FormControl(this.serverSettings.smtpConfig.sizeLimit, [Validators.min(1)]));
      this.settingsForm.addControl('customizedTemplates', new FormControl(this.serverSettings.smtpConfig.customizedTemplates, [Validators.min(1)]));

      // Automatically save settings as we edit them
      this.settingsForm.valueChanges.pipe(
        debounceTime(300),
        distinctUntilChanged(),
        filter(_ => this.settingsForm.valid),
        takeUntilDestroyed(this.destroyRef),
        switchMap(_ => {
          const data = this.packData();
          return this.settingsService.updateServerSettings(data);
        }),
        tap(settings => {
          this.serverSettings = settings;
          this.cdRef.markForCheck();
        })
      ).subscribe();

      this.cdRef.markForCheck();
    });
  }

  resetForm() {
    this.settingsForm.get('hostName')?.setValue(this.serverSettings.hostName);

    this.settingsForm.get('host')?.setValue(this.serverSettings.smtpConfig.host, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('port')?.setValue(this.serverSettings.smtpConfig.port, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('userName')?.setValue(this.serverSettings.smtpConfig.userName, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('enableSsl')?.setValue(this.serverSettings.smtpConfig.enableSsl, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('password')?.setValue(this.serverSettings.smtpConfig.password, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('senderAddress')?.setValue(this.serverSettings.smtpConfig.senderAddress, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('senderDisplayName')?.setValue(this.serverSettings.smtpConfig.senderDisplayName, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('sizeLimit')?.setValue(this.serverSettings.smtpConfig.sizeLimit, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('customizedTemplates')?.setValue(this.serverSettings.smtpConfig.customizedTemplates, {onlySelf: true, emitEvent: false});
    this.settingsForm.markAsPristine();
    this.cdRef.markForCheck();
  }

  autofillGmail() {
    this.settingsForm.get('host')?.setValue('smtp.gmail.com');
    this.settingsForm.get('port')?.setValue(587);
    this.settingsForm.get('sizeLimit')?.setValue(26214400);
    this.settingsForm.get('enableSsl')?.setValue(true);
    this.settingsForm.markAsDirty();
    this.cdRef.markForCheck();
  }

  autofillOutlook() {
    this.settingsForm.get('host')?.setValue('smtp-mail.outlook.com');
    this.settingsForm.get('port')?.setValue(587 );
    this.settingsForm.get('sizeLimit')?.setValue(1048576);
    this.settingsForm.get('enableSsl')?.setValue(true);
    this.settingsForm.markAsDirty();
    this.cdRef.markForCheck();
  }

  packData() {
    const modelSettings = Object.assign({}, this.serverSettings);
    modelSettings.emailServiceUrl = this.settingsForm.get('emailServiceUrl')?.value;
    modelSettings.hostName = this.settingsForm.get('hostName')?.value;

    modelSettings.smtpConfig.host = this.settingsForm.get('host')?.value;
    modelSettings.smtpConfig.port = this.settingsForm.get('port')?.value;
    modelSettings.smtpConfig.userName = this.settingsForm.get('userName')?.value;
    modelSettings.smtpConfig.enableSsl = this.settingsForm.get('enableSsl')?.value;
    modelSettings.smtpConfig.password = this.settingsForm.get('password')?.value;
    modelSettings.smtpConfig.senderAddress = this.settingsForm.get('senderAddress')?.value;
    modelSettings.smtpConfig.senderDisplayName = this.settingsForm.get('senderDisplayName')?.value;
    modelSettings.smtpConfig.sizeLimit = this.settingsForm.get('sizeLimit')?.value;
    modelSettings.smtpConfig.customizedTemplates = this.settingsForm.get('customizedTemplates')?.value;

    return modelSettings;
  }


  test() {
    this.settingsService.testEmailServerSettings().subscribe(res => {
      if (res.successful) {
        this.toastr.success(translate('toasts.email-sent', {email: res.emailAddress}));
      } else {
        this.toastr.error(res.errorMessage);
      }
    });
  }
}
