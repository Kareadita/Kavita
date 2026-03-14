import {ChangeDetectionStrategy, Component, input, OnInit} from '@angular/core';
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule} from "@angular/forms";
import {IHasMetadataIds} from "../../../app/_models/common/i-has-metadata-ids";
import {TranslocoDirective} from "@jsverse/transloco";
import {SettingItemComponent} from "../../../app/settings/_components/setting-item/setting-item.component";

const METADATA_DEFAULTS: Required<IHasMetadataIds> = {
  aniListId: 0,
  malId: 0,
  mangaBakaId: 0,
  hardcoverId: 0,
  comicVineId: null,
  metronId: 0,
};

export type MetadataIdsForm = FormGroup<{
  [K in keyof Required<IHasMetadataIds>]: FormControl<IHasMetadataIds[K] | null>
}>;

@Component({
  selector: 'app-edit-external-metadata',
  imports: [
    TranslocoDirective,
    SettingItemComponent,
    FormsModule,
    ReactiveFormsModule
  ],
  templateUrl: './edit-external-metadata.component.html',
  styleUrl: './edit-external-metadata.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EditExternalMetadataComponent implements OnInit {

  form = input.required<FormGroup>();
  entity = input.required<IHasMetadataIds>();

  protected readonly metadataIds = Object.keys(METADATA_DEFAULTS) as (keyof IHasMetadataIds)[];


  ngOnInit() {
    const form = this.form();
    const entity = this.entity();

    (Object.keys(METADATA_DEFAULTS) as (keyof IHasMetadataIds)[]).forEach((key) => {
      if (!form.contains(key)) {
        form.addControl(key, new FormControl(entity[key] ?? null));
      } else {
        form.get(key)?.setValue(entity[key] ?? null);
      }
    });
  }

  getKeyInputType(key: string) {
    if (key === 'comicVineId') return 'text';
    return 'number';
  }

  getKeyInputMode(key: string) {
    if (key === 'comicVineId') return 'text';
    return 'numeric';
  }

}
