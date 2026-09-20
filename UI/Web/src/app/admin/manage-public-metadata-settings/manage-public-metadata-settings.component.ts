import {ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal, viewChild} from '@angular/core';
import {SettingsService} from "../settings.service";
import {
  ManageMetadataMappingsComponent,
  MetadataMappingsFormModel,
  metadataMappingsSchema,
  packMetadataMappings,
  toMetadataMappingsFormModel
} from "../manage-metadata-mappings/manage-metadata-mappings.component";
import {MetadataSettings} from "../_models/metadata-settings";
import {debounceTime, filter, switchMap} from "rxjs";
import {takeUntilDestroyed, toObservable} from "@angular/core/rxjs-interop";
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
import {apply, form, FormField} from "@angular/forms/signals";

interface FormModel {
  enableExtendedMetadataProcessing: boolean;
  mappings: MetadataMappingsFormModel;
}

function emptyMappings(): MetadataMappingsFormModel {
  return {
    enableGenres: false,
    enableTags: false,
    filterAboveWeight: null,
    blacklist: [],
    whitelist: [],
    ageRatingMappings: [],
    externalAgeRatingMappings: [],
    fieldMappings: [],
  };
}

/**
 * Metadata settings for which a K+ license is not required
 */
@Component({
  selector: 'app-manage-public-metadata-settings',
  imports: [
    ManageMetadataMappingsComponent,
    TranslocoDirective,
    RouterLink,
    SettingSwitchComponent,
    FormField,
  ],
  templateUrl: './manage-public-metadata-settings.component.html',
  styleUrl: './manage-public-metadata-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManagePublicMetadataSettingsComponent implements OnInit {

  readonly manageMetadataMappingsComponent = viewChild(ManageMetadataMappingsComponent);

  private readonly settingService = inject(SettingsService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly licenseService = inject(LicenseService);
  private readonly modalService = inject(ModalService);
  private readonly messageHub = inject(MessageHubService);
  private readonly serverService = inject(ServerService);

  private readonly formModel = signal<FormModel>({
    enableExtendedMetadataProcessing: false,
    mappings: emptyMappings(),
  });
  protected readonly formGroup = form(this.formModel, p => {
    apply(p.mappings, metadataMappingsSchema);
  });

  settings = signal<MetadataSettings | undefined>(undefined);
  isReRunInProgress = signal(true);

  constructor() {
    toObservable(this.formModel).pipe(
      filter(() => this.settings() !== undefined),
      debounceTime(300),
      filter(() => this.formGroup().valid()),
      map(() => this.packData()),
      switchMap((data) => this.settingService.updateMetadataSettings(data)),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe();
  }

  ngOnInit(): void {
    this.settingService.getMetadataSettings().subscribe(settings => {
      this.formModel.set({
        enableExtendedMetadataProcessing: settings.enableExtendedMetadataProcessing,
        mappings: toMetadataMappingsFormModel(settings),
      });
      this.settings.set(settings);
    });

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

  /**
   * Writes the fields this page edits, everything else rides along from the loaded settings.
   */
  packData(): MetadataSettings {
    const {enableExtendedMetadataProcessing, mappings} = this.formModel();

    return {
      ...this.settings()!,
      enableExtendedMetadataProcessing,
      ...packMetadataMappings(mappings),
    };
  }

  reRunMappings() {
    this.modalService.open(RunMetadataMappingsModalComponent, DefaultModalOptions);
  }

  protected readonly SettingsTabId = SettingsTabId;
}
