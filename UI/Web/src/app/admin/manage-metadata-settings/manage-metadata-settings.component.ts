import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {SettingSwitchComponent} from "../../settings/_components/setting-switch/setting-switch.component";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {TagBadgeComponent} from "../../shared/tag-badge/tag-badge.component";
import {SettingsService} from "../settings.service";
import {debounceTime, switchMap, tap} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {map} from "rxjs/operators";


@Component({
  selector: 'app-manage-metadata-settings',
  standalone: true,
  imports: [
    TranslocoDirective,
    ReactiveFormsModule,
    SettingSwitchComponent,
    SettingItemComponent,
    DefaultValuePipe,
    TagBadgeComponent
  ],
  templateUrl: './manage-metadata-settings.component.html',
  styleUrl: './manage-metadata-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageMetadataSettingsComponent implements OnInit {

  private readonly settingService = inject(SettingsService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  settingsForm: FormGroup = new FormGroup({});


  ngOnInit(): void {

    this.settingService.getMetadataSettings().subscribe(settings => {
      this.settingsForm.addControl('enableSummary', new FormControl(settings.enableSummary, []));
      this.settingsForm.addControl('enablePublicationStatus', new FormControl(settings.enablePublicationStatus, []));
      this.settingsForm.addControl('enableRelations', new FormControl(settings.enableRelationships, []));
      this.settingsForm.addControl('enableGenres', new FormControl(settings.enableGenres, []));
      this.settingsForm.addControl('enableTags', new FormControl(settings.enableTags, []));
      this.settingsForm.addControl('enableRelationships', new FormControl(settings.enableRelationships, []));
      this.settingsForm.addControl('enablePeople', new FormControl(settings.enablePeople, []));
      this.settingsForm.addControl('enableStartDate', new FormControl(settings.enableStartDate, []));

      this.settingsForm.addControl('blacklist', new FormControl((settings.blacklist || '').join(','), []));

      this.cdRef.markForCheck();


      this.settingsForm.valueChanges.pipe(
        debounceTime(300),
        takeUntilDestroyed(this.destroyRef),
        map(_ => this.packData()),
        switchMap((data) => this.settingService.updateMetadataSettings(data)),
      ).subscribe();

    });

  }

  packData() {
    const model = this.settingsForm.value;

    // Translate blacklist string -> Array<string>
    return {
      ...model,
      blacklist: (model.blacklist || '').split(',').map((item: string) => item.trim())
    }
  }


}
