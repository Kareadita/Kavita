import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
import { Person } from '../../_models/metadata/person';
import {CommonModule} from "@angular/common";
import {SeriesStaff} from "../../_models/series-detail/external-series-detail";
import {ImageComponent} from "../image/image.component";

@Component({
  selector: 'app-person-badge',
  standalone: true,
  imports: [CommonModule, ImageComponent],
  templateUrl: './person-badge.component.html',
  styleUrls: ['./person-badge.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PersonBadgeComponent implements OnInit {

  @Input({required: true}) person!: Person | SeriesStaff;
  @Input() isStaff = false;

  private readonly cdRef = inject(ChangeDetectorRef);

  staff!: SeriesStaff;

  ngOnInit() {
    this.staff = this.person as SeriesStaff;
    this.cdRef.markForCheck();
  }
}
