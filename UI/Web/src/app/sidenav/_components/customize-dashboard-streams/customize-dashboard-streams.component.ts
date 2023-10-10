import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject} from '@angular/core';
import {CommonModule} from '@angular/common';
import {
  DraggableOrderedListComponent, IndexUpdateEvent
} from "../../../reading-list/_components/draggable-ordered-list/draggable-ordered-list.component";
import {DashboardStreamListItemComponent} from "../dashboard-stream-list-item/dashboard-stream-list-item.component";
import {DashboardStream} from "../../../_models/dashboard/dashboard-stream";
import {SmartFilter} from "../../../_models/metadata/v2/smart-filter";
import {DashboardService} from "../../../_services/dashboard.service";
import {FilterService} from "../../../_services/filter.service";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {forkJoin} from "rxjs";
import {TranslocoDirective} from "@ngneat/transloco";
import {CommonStream} from "../../../_models/common-stream";

@Component({
  selector: 'app-customize-dashboard-streams',
  standalone: true,
  imports: [CommonModule, DraggableOrderedListComponent, DashboardStreamListItemComponent, TranslocoDirective],
  templateUrl: './customize-dashboard-streams.component.html',
  styleUrls: ['./customize-dashboard-streams.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CustomizeDashboardStreamsComponent {

  items: DashboardStream[] = [];
  smartFilters: SmartFilter[] = [];
  accessibilityMode: boolean = false;

  private readonly dashboardService = inject(DashboardService);
  private readonly filterService = inject(FilterService);
  private readonly cdRef = inject(ChangeDetectorRef);

  constructor(public modal: NgbActiveModal) {
    forkJoin([this.dashboardService.getDashboardStreams(false), this.filterService.getAllFilters()]).subscribe(results => {
      this.items = results[0];
      const smartFilterStreams = new Set(results[0].filter(d => !d.isProvided).map(d => d.name));
      this.smartFilters = results[1].filter(d => !smartFilterStreams.has(d.name));
      this.cdRef.markForCheck();
    });
  }

  addFilterToStream(filter: SmartFilter) {
    this.dashboardService.createDashboardStream(filter.id).subscribe(stream => {
      this.smartFilters = this.smartFilters.filter(d => d.name !== filter.name);
      this.items.push(stream);
      this.cdRef.markForCheck();
    });
  }


  orderUpdated(event: IndexUpdateEvent) {
    this.dashboardService.updateDashboardStreamPosition(event.item.name, event.item.id, event.fromPosition, event.toPosition).subscribe();
  }

  updateVisibility(item: DashboardStream, position: number) {
    this.items[position].visible = !this.items[position].visible;
    this.dashboardService.updateDashboardStream(this.items[position]).subscribe();
    this.cdRef.markForCheck();
  }

}
