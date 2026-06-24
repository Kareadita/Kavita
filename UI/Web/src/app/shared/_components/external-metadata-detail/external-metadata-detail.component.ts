import {ChangeDetectionStrategy, Component, computed, input} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {IHasMetadataIds} from "../../../_models/common/i-has-metadata-ids";
import {HAS_METADATA_DEFAULTS} from "../edit-external-metadata-form/edit-external-metadata-form.component";
import {DefaultValuePipe} from "../../../_pipes/default-value.pipe";
import {LabelCardComponent} from "../../../_single-module/label-card/label-card.component";

const URLS = {
  aniListId: 'https://anilist.co/manga/{id}/',
  malId: 'https://myanimelist.net/manga/{id}/',
  mangaBakaId: 'https://mangabaka.org/{id}',
  hardcoverId: 'https://hardcover.app/id/series/{id}',
  comicVineId: null,
  metronId: null,
  cbrId: null,
}

const INDIVIDUAL_URLS = {
  ...URLS,
  hardcoverId: 'https://hardcover.app/id/book/{id}',
}

@Component({
  selector: 'app-external-metadata-detail',
  imports: [
    TranslocoDirective,
    DefaultValuePipe,
    LabelCardComponent
  ],
  templateUrl: './external-metadata-detail.component.html',
  styleUrl: './external-metadata-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExternalMetadataDetailComponent {

  entity = input.required<IHasMetadataIds>();
  /** Extra id to show in this section for details-tab */
  isbn = input<string | null>(null);

  /**
   * Assume ids link to individual works instead of series where applicable
   */
  individualWork = input<boolean>(false);

  metadata = computed(() => {
    const e = this.entity();
    const urls = this.individualWork() ? INDIVIDUAL_URLS : URLS;

    return (Object.keys(HAS_METADATA_DEFAULTS) as (keyof IHasMetadataIds)[]).map(key => {
      const rawValue = e[key];
      const value = rawValue === 0 || rawValue == null ? null : rawValue;

      const urlTemplate = urls[key];
      const linkUrl = urlTemplate && value != null ? urlTemplate.replace('{id}', String(value)) : null;

      return { key, value, linkUrl };
    });
  });
}
