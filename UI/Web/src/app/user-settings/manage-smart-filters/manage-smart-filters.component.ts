import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FilterService} from "../../_services/filter.service";
import {SmartFilter} from "../../_models/metadata/v2/smart-filter";
import {Router} from "@angular/router";

@Component({
  selector: 'app-manage-smart-filters',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './manage-smart-filters.component.html',
  styleUrls: ['./manage-smart-filters.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageSmartFiltersComponent {

  private readonly filterUtility = inject(FilterService);
  private readonly router = inject(Router);
  private readonly cdRef = inject(ChangeDetectorRef);
  filters: Array<SmartFilter> = [];

  constructor() {
    this.filterUtility.getAllFilters().subscribe(filters => {
      this.filters = filters;
      this.cdRef.markForCheck();
    });
  }

  async loadFilter(f: SmartFilter) {
    await this.router.navigateByUrl('all-series?' + f.filter);
  }

}
