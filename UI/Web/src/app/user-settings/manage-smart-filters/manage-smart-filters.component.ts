import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FilterService} from "../../_services/filter.service";
import {SmartFilter} from "../../_models/metadata/v2/smart-filter";
import {Router} from "@angular/router";
import {ConfirmService} from "../../shared/confirm.service";
import {translate} from "@ngneat/transloco";
import {ToastrService} from "ngx-toastr";

@Component({
  selector: 'app-manage-smart-filters',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './manage-smart-filters.component.html',
  styleUrls: ['./manage-smart-filters.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageSmartFiltersComponent {

  private readonly filterService = inject(FilterService);
  private readonly confirmService = inject(ConfirmService);
  private readonly router = inject(Router);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly toastr = inject(ToastrService);
  filters: Array<SmartFilter> = [];

  constructor() {
    this.loadData();
  }

  loadData() {
    this.filterService.getAllFilters().subscribe(filters => {
      this.filters = filters;
      this.cdRef.markForCheck();
    });
  }

  async loadFilter(f: SmartFilter) {
    await this.router.navigateByUrl('all-series?' + f.filter);
  }

  async deleteFilter(f: SmartFilter) {
    if (!await this.confirmService.confirm(translate('toasts.confirm-delete-smart-filter'))) return;

    this.filterService.deleteFilter(f.id).subscribe(() => {
      this.toastr.success(translate('toasts.smart-filter-deleted'));
      this.loadData();
    });
  }

}
