import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  OnInit, signal,
  viewChild
} from '@angular/core';
import {SettingsService} from "../settings.service";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {
  ManageMetadataMappingsComponent,
  MetadataMappingsExport
} from "../manage-metadata-mappings/manage-metadata-mappings.component";
import {MetadataSettings} from "../_models/metadata-settings";
import {debounceTime, filter, switchMap} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {map, tap} from "rxjs/operators";
import {TranslocoDirective} from "@jsverse/transloco";
import {LicenseService} from "../../_services/license.service";
import {SettingSwitchComponent} from "../../settings/_components/setting-switch/setting-switch.component";
import {RouterLink} from "@angular/router";
import {SettingsTabId} from "../../sidenav/preference-nav/preference-nav.component";
import {
  RunMetadataMappingsModalComponent
} from "../manage-metadata-mappings/run-metadata-mappings-modal/run-metadata-mappings-modal.component";
import {DefaultModalOptions} from "../../_models/modal/modal-options";
import {ModalService} from "../../_services/modal.service";
import {EVENTS, MessageHubService} from "../../_services/message-hub.service";
import {NotificationProgressEvent} from "../../_models/events/notification-progress-event";
import {QueueNames, ServerService, TaskMethodNames} from "../../_services/server.service";

/**
 * Metadata settings for which a K+ license is not required
 */
@Component({
  selector: 'app-manage-public-metadata-settings',
  imports: [
    ManageMetadataMappingsComponent,
    TranslocoDirective,
    ReactiveFormsModule,
    RouterLink,
    SettingSwitchComponent,
  ],
  templateUrl: './manage-public-metadata-settings.component.html',
  styleUrl: './manage-public-metadata-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManagePublicMetadataSettingsComponent implements OnInit {

  readonly manageMetadataMappingsComponent = viewChild.required(ManageMetadataMappingsComponent);

  private readonly settingService = inject(SettingsService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly licenseService = inject(LicenseService);
  private readonly modalService = inject(ModalService);
  private readonly messageHub = inject(MessageHubService);
  private readonly serverService = inject(ServerService);

  settingsForm: FormGroup = new FormGroup({});
  settings: MetadataSettings | undefined = undefined;
  isReRunInProgress = signal(true);

  ngOnInit(): void {
    this.settingService.getMetadataSettings().subscribe(settings => {
      this.settings = settings;

      this.settingsForm.addControl('enableExtendedMetadataProcessing', new FormControl(this.settings.enableExtendedMetadataProcessing, []));
      this.cdRef.markForCheck();
    });

    this.settingsForm.valueChanges.pipe(
      debounceTime(300),
      takeUntilDestroyed(this.destroyRef),
      map(_ => this.packData()),
      switchMap((data) => this.settingService.updateMetadataSettings(data)),
    ).subscribe();

    this.serverService.isTaskRunning(TaskMethodNames.RunMetadataMappings, QueueNames.Scan).pipe(
      tap(b => this.isReRunInProgress.set(b))
    ).subscribe();

    this.messageHub.messages$.pipe(
      takeUntilDestroyed(this.destroyRef),
      filter(e => e.event === EVENTS.NotificationProgress),
      map(e => e.payload as NotificationProgressEvent),
      filter(e => e.name === EVENTS.RerunMetadataMappingsProgress),
      map(e => e.eventType !== 'ended'),
      tap(inProgress => this.isReRunInProgress.set(inProgress))
    ).subscribe();
  }

  packData() {
    const model = Object.assign({}, this.settings);
    const formValue = this.settingsForm.value;

    const exp: MetadataMappingsExport = this.manageMetadataMappingsComponent().packData()

    model.enableExtendedMetadataProcessing = formValue.enableExtendedMetadataProcessing;
    model.ageRatingMappings = exp.ageRatingMappings;
    model.fieldMappings = exp.fieldMappings;
    model.whitelist = exp.whitelist;
    model.blacklist = exp.blacklist;

    return model;
  }

  reRunMappings() {
    this.modalService.open(RunMetadataMappingsModalComponent, DefaultModalOptions);
  }

  protected readonly SettingsTabId = SettingsTabId;
}
