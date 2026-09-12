import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, signal} from '@angular/core';
import {
  DraggableOrderedListComponent,
  IndexUpdateEvent
} from "../../../reading-list/_components/draggable-ordered-list/draggable-ordered-list.component";
import {DashboardStreamListItemComponent} from "../dashboard-stream-list-item/dashboard-stream-list-item.component";
import {DashboardStream} from "../../../_models/dashboard/dashboard-stream";
import {SmartFilter} from "../../../_models/metadata/v2/smart-filter";
import {DashboardService} from "../../../_services/dashboard.service";
import {FilterService} from "../../../_services/filter.service";
import {forkJoin} from "rxjs";
import {TranslocoDirective} from "@jsverse/transloco";
import {FilterPipe} from "../../../_pipes/filter.pipe";
import {BreakpointService} from "../../../_services/breakpoint.service";
import {FilterFieldComponent} from "../../../shared/_components/filter-field/filter-field.component";
import {matchesQuery} from "../../../_helpers/filtered";

@Component({
    selector: 'app-customize-dashboard-streams',
    imports: [DraggableOrderedListComponent, DashboardStreamListItemComponent, TranslocoDirective,
      FilterPipe, FilterFieldComponent],
    templateUrl: './customize-dashboard-streams.component.html',
    styleUrls: ['./customize-dashboard-streams.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class CustomizeDashboardStreamsComponent {

  private readonly dashboardService = inject(DashboardService);
  private readonly filterService = inject(FilterService);
  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly breakpointService = inject(BreakpointService);

  items: DashboardStream[] = [];
  allSmartFilters: SmartFilter[] = [];
  smartFilters: SmartFilter[] = [];
  accessibilityMode: boolean = false;
  filterQuery = signal('');

  filterList = (listItem: SmartFilter) => matchesQuery(listItem, this.filterQuery(), 'name');

  constructor() {
    forkJoin([this.dashboardService.getDashboardStreams(false), this.filterService.getAllFilters()]).subscribe(results => {
      this.items = results[0];

      // After 100 items, drag and drop is disabled to use virtualization
      if (this.items.length > 100 || this.breakpointService.isTabletOrBelow()) {
        this.accessibilityMode = true;
      }

      this.allSmartFilters = results[1];
      this.updateSmartFilters();

      this.cdRef.markForCheck();
    });
  }

  updateSmartFilters() {
    const smartFilterStreams = new Set(this.items.filter(d => !d.isProvided).map(d => d.name));
    this.smartFilters = this.allSmartFilters.filter(d => !smartFilterStreams.has(d.name));
    this.cdRef.markForCheck();
  }

  addFilterToStream(filter: SmartFilter) {
    this.dashboardService.createDashboardStream(filter.id).subscribe(stream => {
      this.smartFilters = this.smartFilters.filter(d => d.name !== filter.name);
      this.items = [...this.items, stream];
      this.cdRef.markForCheck();
    });
  }


  orderUpdated(event: IndexUpdateEvent) {
    this.dashboardService.updateDashboardStreamPosition(event.item.name, event.item.id, event.fromPosition, event.toPosition).subscribe();
  }

  updateVisibility(item: DashboardStream, position: number) {
    this.items[position].visible = !this.items[position].visible;
    this.cdRef.markForCheck();
    this.dashboardService.updateDashboardStream(this.items[position]).subscribe();
  }

  delete(item: DashboardStream) {
    this.dashboardService.deleteSmartFilterStream(item.id).subscribe({
      next: () => {
        this.items = this.items.filter(d => d.id !== item.id);
        this.updateSmartFilters();
        this.cdRef.markForCheck();
      },
      error: (err) => {
        console.error(err);
      }
    });
  }

}
