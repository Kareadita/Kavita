import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
import { Person } from '../../_models/metadata/person';
import {CommonModule} from "@angular/common";
import {SeriesStaff} from "../../_models/series-detail/external-series-detail";
import {ImageComponent} from "../image/image.component";
import {ImageService} from "../../_services/image.service";
import {RouterLink} from "@angular/router";

@Component({
  selector: 'app-person-badge',
  standalone: true,
  imports: [ImageComponent, RouterLink],
  templateUrl: './person-badge.component.html',
  styleUrls: ['./person-badge.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PersonBadgeComponent implements OnInit {

  protected readonly imageService = inject(ImageService);
  private readonly cdRef = inject(ChangeDetectorRef);

  @Input({required: true}) person!: Person | SeriesStaff;
  @Input() isStaff = false;

  staff!: SeriesStaff;

  get HasCoverImage() {
    return this.isStaff || (this.person as Person).coverImage;
  }

  get ImageUrl() {
    if (this.isStaff && this.staff.imageUrl && !this.staff.imageUrl.endsWith('default.jpg')) {
      return (this.person as SeriesStaff).imageUrl || '';
    }
    return this.imageService.getPersonImage((this.person as Person).id);
  }

  ngOnInit() {
    this.staff = this.person as SeriesStaff;
    this.cdRef.markForCheck();
  }

    protected readonly encodeURIComponent = encodeURIComponent;
}
