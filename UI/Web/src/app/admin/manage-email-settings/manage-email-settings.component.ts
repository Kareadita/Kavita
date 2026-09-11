import {ChangeDetectionStrategy, Component, inject, OnInit, signal} from '@angular/core';
import {ToastrService} from '@openng/ngx-toastr';
import {debounceTime, distinctUntilChanged, filter, switchMap} from 'rxjs';
import {SettingsService} from '../settings.service';
import {ServerSettings} from '../_models/server-settings';
import {translate, TranslocoModule} from "@jsverse/transloco";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {SettingSwitchComponent} from "../../settings/_components/setting-switch/setting-switch.component";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {BytesPipe} from "../../_pipes/bytes.pipe";
import {toObservable} from "@angular/core/rxjs-interop";
import {EnterBlurDirective} from "../../_directives/enter-blur.directive";
import {form, FormField, min, pattern} from "@angular/forms/signals";
import {FormFieldDirective} from "../../_directives/form-field.directive";

interface FormModel {
  hostName: string;
  host: string;
  port: number;
  userName: string;
  enableSsl: boolean;
  password: string;
  senderAddress: string;
  senderDisplayName: string;
  sizeLimit: number;
  customizedTemplates: boolean;
}

@Component({
    selector: 'app-manage-email-settings',
    templateUrl: './manage-email-settings.component.html',
    styleUrls: ['./manage-email-settings.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoModule, SettingItemComponent, SettingSwitchComponent, DefaultValuePipe, BytesPipe, EnterBlurDirective, FormFieldDirective, FormField]
})
export class ManageEmailSettingsComponent implements OnInit {

  private readonly settingsService = inject(SettingsService);
  private readonly toastr = inject(ToastrService);

  serverSettings = signal<ServerSettings>({} as ServerSettings);
  formModel = signal<FormModel>({
    customizedTemplates: false,
    enableSsl: false,
    host: "",
    hostName: "",
    password: "",
    port: 0,
    senderAddress: "",
    senderDisplayName: "",
    sizeLimit: 10,
    userName: ""
  });
  formGroup = form(this.formModel, path => {
    min(path.sizeLimit, 1);
  });

  constructor() {
    toObservable(this.formModel).pipe(
      distinctUntilChanged(),
      debounceTime(300),
      filter(_ => this.formGroup().valid()),
      switchMap(() => this.settingsService.updateServerSettings({
        ...this.serverSettings(),
        hostName: this.formModel().hostName,
        smtpConfig: this.formModel(),
      }))
    ).subscribe();
  }

  ngOnInit(): void {
    this.settingsService.getServerSettings().subscribe((settings: ServerSettings) => {
      this.serverSettings.set(settings);
      this.formModel.set({
        hostName: settings.hostName,
        ...settings.smtpConfig,
      });
    });
  }

  resetForm() {
    const settings = this.serverSettings();
    this.formModel.set({
      hostName: settings.hostName,
      ...settings.smtpConfig,
    });
  }

  autofillGmail() {
    this.formModel.update(x => ({
      ...x,
      host: 'smtp.gmail.com',
      port: 587,
      sizeLimit: 26214400,
      enableSsl: true,
    }));
  }

  autofillOutlook() {
    this.formModel.update(x => ({
      ...x,
      host: 'smtp-mail.outlook.com',
      port: 587,
      sizeLimit: 1048576,
      enableSsl: true,
    }));
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

  protected readonly pattern = pattern;
}
