import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject} from '@angular/core';
import {CommonModule} from '@angular/common';
import {SmartFilter} from "../../../_models/metadata/v2/smart-filter";
import {FilterService} from "../../../_services/filter.service";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {forkJoin} from "rxjs";
import {
  DraggableOrderedListComponent,
  IndexUpdateEvent
} from "../../../reading-list/_components/draggable-ordered-list/draggable-ordered-list.component";
import {SideNavStream} from "../../../_models/sidenav/sidenav-stream";
import {NavService} from "../../../_services/nav.service";
import {DashboardStreamListItemComponent} from "../dashboard-stream-list-item/dashboard-stream-list-item.component";
import {CommonStream} from "../../../_models/common-stream";
import {TranslocoDirective} from "@ngneat/transloco";
import {SidenavStreamListItemComponent} from "../sidenav-stream-list-item/sidenav-stream-list-item.component";
import {ExternalSourceService} from "../../../external-source.service";
import {ExternalSource} from "../../../_models/sidenav/external-source";

@Component({
  selector: 'app-customize-sidenav-streams',
  standalone: true,
  imports: [CommonModule, DraggableOrderedListComponent, DashboardStreamListItemComponent, TranslocoDirective, SidenavStreamListItemComponent],
  templateUrl: './customize-sidenav-streams.component.html',
  styleUrls: ['./customize-sidenav-streams.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CustomizeSidenavStreamsComponent {

  items: SideNavStream[] = [];
  smartFilters: SmartFilter[] = [];
  externalSources: ExternalSource[] = [];
  accessibilityMode: boolean = false;

  private readonly sideNavService = inject(NavService);
  private readonly filterService = inject(FilterService);
  private readonly externalSourceService = inject(ExternalSourceService);
  private readonly cdRef = inject(ChangeDetectorRef);

  constructor(public modal: NgbActiveModal) {
    forkJoin([this.sideNavService.getSideNavStreams(false), this.filterService.getAllFilters(), this.externalSourceService.getExternalSources()]).subscribe(results => {
      this.items = results[0];
      const nonProvidedStreams = new Set(results[0].filter(d => !d.isProvided).map(d => d.name));
      this.smartFilters = results[1].filter(d => !nonProvidedStreams.has(d.name));

      this.externalSources = results[2].filter(d => !nonProvidedStreams.has(d.name));
      this.cdRef.markForCheck();
    });
  }

  addFilterToStream(filter: SmartFilter) {
    this.sideNavService.createSideNavStream(filter.id).subscribe(stream => {
      this.smartFilters = this.smartFilters.filter(d => d.name !== filter.name);
      this.items.push(stream);
      this.cdRef.markForCheck();
    });
  }

    addExternalSourceToStream(externalSource: ExternalSource) {
        this.sideNavService.createSideNavStreamFromExternalSource(externalSource.id).subscribe(stream => {
            this.externalSources = this.externalSources.filter(d => d.name !== externalSource.name);
            this.items.push(stream);
            this.cdRef.markForCheck();
        });
    }


  orderUpdated(event: IndexUpdateEvent) {
    this.sideNavService.updateSideNavStreamPosition(event.item.name, event.item.id, event.fromPosition, event.toPosition).subscribe();
  }

  updateVisibility(item: SideNavStream, position: number) {
    this.items[position].visible = !this.items[position].visible;
    this.sideNavService.updateSideNavStream(this.items[position]).subscribe();
    this.cdRef.markForCheck();
  }

}
