import {ChangeDetectionStrategy, Component, computed, input} from '@angular/core';
import {disabled, FieldTree, FormField, SchemaPathTree} from "@angular/forms/signals";
import {IHasMetadataIds} from "../../../_models/common/i-has-metadata-ids";
import {TranslocoDirective} from "@jsverse/transloco";
import {SettingItemComponent} from "../../../settings/_components/setting-item/setting-item.component";
import {FormFieldDirective} from "../../../_directives/form-field.directive";

export const HAS_METADATA_DEFAULTS: Required<IHasMetadataIds> = {
  aniListId: 0,
  malId: 0,
  mangaBakaId: 0,
  hardcoverId: 0,
  comicVineId: null,
  metronId: 0,
  cbrId: 0
};

type MetadataIdKey = keyof IHasMetadataIds;

/** Call from the schema of any form whose model carries the metadata ids */
export function applyExternalMetadataIdRules(p: SchemaPathTree<IHasMetadataIds>): void {
  disabled(p.cbrId);
}

@Component({
  selector: 'app-edit-external-metadata-form',
  imports: [
    TranslocoDirective,
    SettingItemComponent,
    FormFieldDirective,
    FormField,
  ],
  templateUrl: './edit-external-metadata-form.component.html',
  styleUrl: './edit-external-metadata-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EditExternalMetadataFormComponent<T extends IHasMetadataIds> {

  field = input.required<FieldTree<T>>();

  protected readonly metadataIds = Object.keys(HAS_METADATA_DEFAULTS) as MetadataIdKey[];

  protected readonly subFields = computed(() => {
    const tree = this.field();

    return this.metadataIds.map(key => {
      const field = tree[key] as FieldTree<unknown>;
      // [formField] resolves the value type off the input element, so each branch needs the field typed to match
      return {
        key,
        field,
        numberField: field as FieldTree<number | null>,
        textField: field as FieldTree<string>,
      };
    });
  });

  getKeyInputType(key: string) {
    if (key === 'comicVineId') return 'text';
    return 'number';
  }
}
