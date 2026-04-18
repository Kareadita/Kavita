import {ChangeDetectionStrategy, Component, inject, OnInit, signal} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {
  SideNavCompanionBarComponent
} from "../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";
import {SmartFilter} from "../_models/metadata/v2/smart-filter";
import {FilterService} from "../_services/filter.service";
import {ManageSmartFiltersComponent} from "../sidenav/_components/manage-smart-filters/manage-smart-filters.component";
import {APP_BASE_HREF, DecimalPipe} from "@angular/common";

@Component({
  selector: 'app-all-filters',
  imports: [TranslocoDirective, SideNavCompanionBarComponent, ManageSmartFiltersComponent, DecimalPipe],
  templateUrl: './all-filters.component.html',
  styleUrl: './all-filters.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AllFiltersComponent implements OnInit {
  private readonly filterService = inject(FilterService);
  protected readonly baseUrl = inject(APP_BASE_HREF);

  filters = signal<SmartFilter[]>([]);

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.filterService.getAllFilters().subscribe(filters => {
      this.filters.set(filters);
    });
  }
}
