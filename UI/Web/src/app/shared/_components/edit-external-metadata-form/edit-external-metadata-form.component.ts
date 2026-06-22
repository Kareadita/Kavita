import {ChangeDetectionStrategy, Component, input, OnInit} from '@angular/core';
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule} from "@angular/forms";
import {IHasMetadataIds} from "../../../_models/common/i-has-metadata-ids";
import {TranslocoDirective} from "@jsverse/transloco";
import {SettingItemComponent} from "../../../settings/_components/setting-item/setting-item.component";

export const HAS_METADATA_DEFAULTS: Required<IHasMetadataIds> = {
  aniListId: 0,
  malId: 0,
  mangaBakaId: 0,
  hardcoverId: 0,
  comicVineId: null,
  metronId: 0,
  cbrId: 0
};

@Component({
  selector: 'app-edit-external-metadata-form',
  imports: [
    TranslocoDirective,
    SettingItemComponent,
    FormsModule,
    ReactiveFormsModule
  ],
  templateUrl: './edit-external-metadata-form.component.html',
  styleUrl: './edit-external-metadata-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EditExternalMetadataFormComponent implements OnInit {

  form = input.required<FormGroup>();
  entity = input.required<IHasMetadataIds>();

  protected readonly metadataIds = Object.keys(HAS_METADATA_DEFAULTS) as (keyof IHasMetadataIds)[];


  ngOnInit() {
    const form = this.form();
    const entity = this.entity();

    (Object.keys(HAS_METADATA_DEFAULTS) as (keyof IHasMetadataIds)[]).forEach((key) => {
      if (!form.contains(key)) {
        const control = new FormControl(entity[key] ?? null);
        if (this.getIsDisabled(key)) {
          control.disable();
        }
        form.addControl(key, control);
      } else {
        form.get(key)?.setValue(entity[key] ?? null);
      }
    });
  }

  getKeyInputType(key: string) {
    if (key === 'comicVineId') return 'text';
    return 'number';
  }

  getIsDisabled(key: string) {
    if (key === 'cbrId') return true;
    return false;
  }
}
