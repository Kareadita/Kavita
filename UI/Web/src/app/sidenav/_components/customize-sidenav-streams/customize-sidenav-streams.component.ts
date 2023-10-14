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
import {StreamType} from "../../../_models/dashboard/stream-type.enum";
import {SideNavStreamType} from "../../../_models/sidenav/sidenav-stream-type.enum";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {FilterPipe} from "../../../pipe/filter.pipe";

@Component({
  selector: 'app-customize-sidenav-streams',
  standalone: true,
  imports: [CommonModule, DraggableOrderedListComponent, DashboardStreamListItemComponent, TranslocoDirective, SidenavStreamListItemComponent, ReactiveFormsModule, FilterPipe],
  templateUrl: './customize-sidenav-streams.component.html',
  styleUrls: ['./customize-sidenav-streams.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CustomizeSidenavStreamsComponent {

  items: SideNavStream[] = [];
  smartFilters: SmartFilter[] = [];
  externalSources: ExternalSource[] = [];
  accessibilityMode: boolean = false;

  listForm: FormGroup = new FormGroup({
    'filterSideNavStream': new FormControl('', []),
    'filterSmartFilter': new FormControl('', []),
    'filterExternalSource': new FormControl('', []),
  });

  filterSideNavStreams = (listItem: SideNavStream) => {
    const filterVal = (this.listForm.value.filterSideNavStream || '').toLowerCase();
    return listItem.name.toLowerCase().indexOf(filterVal) >= 0;
  }

  filterSmartFilters = (listItem: SmartFilter) => {
    const filterVal = (this.listForm.value.filterSmartFilter || '').toLowerCase();
    return listItem.name.toLowerCase().indexOf(filterVal) >= 0;
  }

  filterExternalSources = (listItem: ExternalSource) => {
    const filterVal = (this.listForm.value.filterExternalSource || '').toLowerCase();
    return listItem.name.toLowerCase().indexOf(filterVal) >= 0;
  }


  private readonly sideNavService = inject(NavService);
  private readonly filterService = inject(FilterService);
  private readonly externalSourceService = inject(ExternalSourceService);
  private readonly cdRef = inject(ChangeDetectorRef);

  constructor(public modal: NgbActiveModal) {
    forkJoin([this.sideNavService.getSideNavStreams(false),
        this.filterService.getAllFilters(), this.externalSourceService.getExternalSources()
    ]).subscribe(results => {
      this.items = results[0];
      const existingSmartFilterStreams = new Set(results[0].filter(d => !d.isProvided && d.streamType === SideNavStreamType.SmartFilter).map(d => d.name));
      this.smartFilters = results[1].filter(d => !existingSmartFilterStreams.has(d.name));

      const existingExternalSourceStreams = new Set(results[0].filter(d => !d.isProvided && d.streamType === SideNavStreamType.ExternalSource).map(d => d.name));
      this.externalSources = results[2].filter(d => !existingExternalSourceStreams.has(d.name));
      this.cdRef.markForCheck();
    });
  }

  resetSideNavFilter() {
    this.listForm.get('filterSideNavStream')?.setValue('');
    this.cdRef.markForCheck();
  }

  resetSmartFilterFilter() {
    this.listForm.get('filterSmartFilter')?.setValue('');
    this.cdRef.markForCheck();
  }

  resetExternalSourceFilter() {
    this.listForm.get('filterExternalSource')?.setValue('');
    this.cdRef.markForCheck();
  }

  addFilterToStream(filter: SmartFilter) {
    this.sideNavService.createSideNavStream(filter.id).subscribe(stream => {
      this.smartFilters = this.smartFilters.filter(d => d.name !== filter.name);
      this.items = [...this.items, stream];
      this.cdRef.markForCheck();
    });
  }

  addExternalSourceToStream(externalSource: ExternalSource) {
    this.sideNavService.createSideNavStreamFromExternalSource(externalSource.id).subscribe(stream => {
      this.externalSources = this.externalSources.filter(d => d.name !== externalSource.name);
      this.items = [...this.items, stream];
      this.cdRef.markForCheck();
    });
  }

  updateAccessibilityMode() {
    this.accessibilityMode = !this.accessibilityMode;
    this.cdRef.markForCheck();
  }


  orderUpdated(event: IndexUpdateEvent) {
    this.sideNavService.updateSideNavStreamPosition(event.item.name, event.item.id, event.fromPosition, event.toPosition).subscribe(() => {
      if (event.fromAccessibilityMode) {
        this.sideNavService.getSideNavStreams(false).subscribe((data) => {
          this.items = [...data];
          this.cdRef.markForCheck();
        })
      }
    });
  }

  updateVisibility(item: SideNavStream, position: number) {
    const stream = this.items.filter(s => s.id == item.id)[0];
    stream.visible = !stream.visible;
    this.sideNavService.updateSideNavStream(stream).subscribe();
    this.cdRef.markForCheck();
  }

}
