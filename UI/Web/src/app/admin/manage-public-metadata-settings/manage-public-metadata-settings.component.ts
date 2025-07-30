import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  OnInit,
  ViewChild
} from '@angular/core';
import {SettingsService} from "../settings.service";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {
  ManageMetadataMappingsComponent,
  MetadataMappingsExport
} from "../manage-metadata-mappings/manage-metadata-mappings.component";
import {MetadataSettings} from "../_models/metadata-settings";
import {debounceTime, switchMap} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {map} from "rxjs/operators";
import {TranslocoDirective} from "@jsverse/transloco";
import {LicenseService} from "../../_services/license.service";
import {SettingSwitchComponent} from "../../settings/_components/setting-switch/setting-switch.component";
import {RouterLink} from "@angular/router";
import {SettingsTabId} from "../../sidenav/preference-nav/preference-nav.component";

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

  @ViewChild(ManageMetadataMappingsComponent) manageMetadataMappingsComponent!: ManageMetadataMappingsComponent;

  private readonly settingService = inject(SettingsService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly licenseService = inject(LicenseService);

  settingsForm: FormGroup = new FormGroup({});
  settings: MetadataSettings | undefined = undefined;

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
  }

  packData() {
    const model = Object.assign({}, this.settings);
    const formValue = this.settingsForm.value;

    const exp: MetadataMappingsExport = this.manageMetadataMappingsComponent.packData()

    model.enableExtendedMetadataProcessing = formValue.enableExtendedMetadataProcessing;
    model.ageRatingMappings = exp.ageRatingMappings;
    model.fieldMappings = exp.fieldMappings;
    model.whitelist = exp.whitelist;
    model.blacklist = exp.blacklist;

    return model;
  }

  protected readonly SettingsTabId = SettingsTabId;
}
