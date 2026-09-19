import {ChangeDetectionStrategy, Component, computed, inject, signal} from '@angular/core';
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
import {BreakpointService} from "../../../_services/breakpoint.service";
import {FilterFieldComponent} from "../../../shared/_components/filter-field/filter-field.component";
import {filteredBy} from "../../../_helpers/filtered";

@Component({
    selector: 'app-customize-dashboard-streams',
    imports: [DraggableOrderedListComponent, DashboardStreamListItemComponent, TranslocoDirective, FilterFieldComponent],
    templateUrl: './customize-dashboard-streams.component.html',
    styleUrls: ['./customize-dashboard-streams.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class CustomizeDashboardStreamsComponent {

  private readonly dashboardService = inject(DashboardService);
  private readonly filterService = inject(FilterService);
  private readonly breakpointService = inject(BreakpointService);

  protected readonly virtualizeAfter = 100;

  private readonly allSmartFilters = signal<SmartFilter[]>([]);
  items = signal<DashboardStream[]>([]);
  accessibilityMode = signal(false);
  filterQuery = signal('');

  protected readonly smartFilters = computed(() => {
    const streamed = new Set(this.items().filter(d => !d.isProvided).map(d => d.name));
    return this.allSmartFilters().filter(d => !streamed.has(d.name));
  });

  protected readonly filteredSmartFilters = filteredBy(this.smartFilters, this.filterQuery, 'name');
  protected readonly showFilter = computed(() => this.smartFilters().length > 3);

  constructor() {
    forkJoin([this.dashboardService.getDashboardStreams(false), this.filterService.getAllFilters()]).subscribe(results => {
      this.items.set(results[0]);

      // After X items, drag and drop is disabled to use virtualization
      if (results[0].length > this.virtualizeAfter || this.breakpointService.isTabletOrBelow()) {
        this.accessibilityMode.set(true);
      }

      this.allSmartFilters.set(results[1]);
    });
  }

  addFilterToStream(filter: SmartFilter) {
    this.dashboardService.createDashboardStream(filter.id).subscribe(stream => {
      this.items.update(items => [...items, stream]);
    });
  }

  orderUpdated(event: IndexUpdateEvent) {
    this.dashboardService.updateDashboardStreamPosition(event.item.name, event.item.id, event.fromPosition, event.toPosition).subscribe();
  }

  updateVisibility(item: DashboardStream) {
    const updated = {...item, visible: !item.visible};
    this.items.update(items => items.map(s => s.id === item.id ? updated : s));
    this.dashboardService.updateDashboardStream(updated).subscribe();
  }

  delete(item: DashboardStream) {
    this.dashboardService.deleteSmartFilterStream(item.id).subscribe({
      next: () => {
        this.items.update(items => items.filter(d => d.id !== item.id));
      },
      error: (err) => {
        console.error(err);
      }
    });
  }

}
