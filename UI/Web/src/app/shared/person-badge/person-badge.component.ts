import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {Person} from '../../_models/metadata/person';

import {SeriesStaff} from "../../_models/series-detail/external-series-detail";
import {ImageComponent} from "../image/image.component";
import {ImageService} from "../../_services/image.service";
import {RouterLink} from "@angular/router";

@Component({
    selector: 'app-person-badge',
    imports: [ImageComponent, RouterLink],
    templateUrl: './person-badge.component.html',
    styleUrls: ['./person-badge.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class PersonBadgeComponent {

  protected readonly imageService = inject(ImageService);

  person = input.required<Person | SeriesStaff>();
  isStaff = input(false);

  staff = computed(() => this.person() as SeriesStaff);

  hasCoverImage = computed(() => {
    return this.isStaff() || !!(this.person() as Person).coverImage;
  });

  imageUrl = computed(() => {
    if (this.isStaff()) {
      const s = this.staff();
      if (s.imageUrl && !s.imageUrl.endsWith('default.jpg')) {
        return s.imageUrl;
      }
    }
    return this.imageService.getPersonImage((this.person() as Person).id);
  });

  protected readonly encodeURIComponent = encodeURIComponent;
}
