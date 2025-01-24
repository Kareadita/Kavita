import {ChangeDetectionStrategy, Component, OnInit} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {SettingSwitchComponent} from "../../settings/_components/setting-switch/setting-switch.component";

@Component({
  selector: 'app-manage-metadata-settings',
  standalone: true,
  imports: [
    TranslocoDirective,
    ReactiveFormsModule,
    SettingSwitchComponent
  ],
  templateUrl: './manage-metadata-settings.component.html',
  styleUrl: './manage-metadata-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageMetadataSettingsComponent implements OnInit {

  settingsForm: FormGroup = new FormGroup({});


  ngOnInit(): void {
    this.settingsForm.addControl('allowSummary', new FormControl(true, [Validators.required]));
    this.settingsForm.addControl('derivePublicationStatus', new FormControl(true, [Validators.required]));
    this.settingsForm.addControl('allowRelations', new FormControl(true, [Validators.required]));
    this.settingsForm.addControl('allowGenres', new FormControl(true, [Validators.required]));

    this.settingsForm.addControl('genreBlacklist', new FormControl(true, [Validators.required]));
  }


}
