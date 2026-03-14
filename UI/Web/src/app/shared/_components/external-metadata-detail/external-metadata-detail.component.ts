import {ChangeDetectionStrategy, Component, input} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {IHasMetadataIds} from "../../../_models/common/i-has-metadata-ids";
import {HAS_METADATA_DEFAULTS} from "../edit-external-metadata-form/edit-external-metadata-form.component";
import {DefaultValuePipe} from "../../../_pipes/default-value.pipe";

@Component({
  selector: 'app-external-metadata-detail',
  imports: [
    TranslocoDirective,
    DefaultValuePipe
  ],
  templateUrl: './external-metadata-detail.component.html',
  styleUrl: './external-metadata-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExternalMetadataDetailComponent {

  entity = input.required<IHasMetadataIds>();
  protected readonly metadataIds = Object.keys(HAS_METADATA_DEFAULTS) as (keyof IHasMetadataIds)[];

}
