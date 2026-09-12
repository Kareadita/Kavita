import {ChangeDetectionStrategy, Component, computed, effect, inject, OnDestroy, signal} from '@angular/core';
import {SmartFilter} from "../../../_models/metadata/v2/smart-filter";
import {FilterService} from "../../../_services/filter.service";
import {forkJoin} from "rxjs";
import {
  DraggableOrderedListComponent,
  IndexUpdateEvent
} from "../../../reading-list/_components/draggable-ordered-list/draggable-ordered-list.component";
import {SideNavStream} from "../../../_models/sidenav/sidenav-stream";
import {NavService} from "../../../_services/nav.service";
import {TranslocoDirective} from "@jsverse/transloco";
import {SidenavStreamListItemComponent} from "../sidenav-stream-list-item/sidenav-stream-list-item.component";
import {ExternalSourceService} from "../../../_services/external-source.service";
import {ExternalSource} from "../../../_models/sidenav/external-source";
import {SideNavStreamType} from "../../../_models/sidenav/sidenav-stream-type.enum";
import {BulkOperationsComponent} from "../../../cards/bulk-operations/bulk-operations.component";
import {BulkSelectionService} from "../../../cards/bulk-selection.service";
import {BreakpointService} from "../../../_services/breakpoint.service";
import {ActionResult} from "../../../_models/actionables/action-result";
import {FilterFieldComponent} from "../../../shared/_components/filter-field/filter-field.component";
import {filteredBy} from "../../../_helpers/filtered";
import {disabled, form, FormField} from "@angular/forms/signals";

interface FormModel {
  accessibilityMode: boolean;
  bulkMode: boolean;
}

@Component({
  selector: 'app-customize-sidenav-streams',
  imports: [DraggableOrderedListComponent, TranslocoDirective, SidenavStreamListItemComponent,
    BulkOperationsComponent, FilterFieldComponent, FormField],
  templateUrl: './customize-sidenav-streams.component.html',
  styleUrls: ['./customize-sidenav-streams.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CustomizeSidenavStreamsComponent implements OnDestroy {

  private readonly sideNavService = inject(NavService);
  private readonly filterService = inject(FilterService);
  private readonly externalSourceService = inject(ExternalSourceService);
  public readonly bulkSelectionService = inject(BulkSelectionService);
  private readonly breakpointService = inject(BreakpointService);

  protected readonly virtualizeAfter = 100;

  protected readonly items = signal<SideNavStream[]>([]);
  private readonly allSmartFilters = signal<SmartFilter[]>([]);
  private readonly allExternalSources = signal<ExternalSource[]>([]);

  protected readonly sideNavStreamQuery = signal('');
  protected readonly smartFilterQuery = signal('');
  protected readonly externalSourceQuery = signal('');

  private readonly formModel = signal<FormModel>({
    accessibilityMode: false,
    bulkMode: false,
  });
  protected readonly formGroup = form(this.formModel, (p) => {
    disabled(p.bulkMode, {when: ({valueOf}) => valueOf(p.accessibilityMode)});
    disabled(p.accessibilityMode, {when: ({valueOf}) => valueOf(p.bulkMode)});
  });

  protected readonly smartFilters = computed(() => {
    const streamed = new Set(this.items()
      .filter(d => !d.isProvided && d.streamType === SideNavStreamType.SmartFilter)
      .map(d => d.name));
    return this.allSmartFilters().filter(d => !streamed.has(d.name));
  });

  protected readonly externalSources = computed(() => {
    const streamed = new Set(this.items()
      .filter(d => !d.isProvided && d.streamType === SideNavStreamType.ExternalSource)
      .map(d => d.name));
    return this.allExternalSources().filter(d => !streamed.has(d.name));
  });

  protected readonly filteredItems = filteredBy(this.items, this.sideNavStreamQuery, 'name');
  protected readonly filteredSmartFilters = filteredBy(this.smartFilters, this.smartFilterQuery, 'name');
  protected readonly filteredExternalSources = filteredBy(this.externalSources, this.externalSourceQuery, 'name', 'host');

  protected readonly filterDisabled = computed(() => this.formModel().accessibilityMode || this.formModel().bulkMode);

  constructor() {

    effect(() => {
      if (this.formModel().bulkMode) return;
      this.bulkSelectionService.deselectAll();
    });

    this.bulkSelectionService.registerDataSource('sideNavStream', () => this.items());
    this.bulkSelectionService.registerPostAction((result: ActionResult<SideNavStream[]>) => {

      const updatedStreams = result.entity;

      this.items.update(items => items.map(item => {
        const updated = updatedStreams.find(u => u.id === item.id);
        return updated ? {...updated} : item;
      }));
    })

    forkJoin([this.sideNavService.getSideNavStreams(false),
        this.filterService.getAllFilters(), this.externalSourceService.getExternalSources()
    ]).subscribe(results => {
      this.items.set(results[0]);

      // After X items, drag and drop is disabled to use virtualization
      if (results[0].length > this.virtualizeAfter || this.breakpointService.isTabletOrBelow()) {
        this.formModel.update(m => ({...m, accessibilityMode: true}));
      }

      this.allSmartFilters.set(results[1]);
      this.allExternalSources.set(results[2]);
    });
  }

  ngOnDestroy() {
    this.bulkSelectionService.deselectAll();
  }

  addFilterToStream(filter: SmartFilter) {
    this.sideNavService.createSideNavStream(filter.id).subscribe(stream => {
      this.items.update(items => [...items, stream]);
    });
  }

  addExternalSourceToStream(externalSource: ExternalSource) {
    this.sideNavService.createSideNavStreamFromExternalSource(externalSource.id).subscribe(stream => {
      this.items.update(items => [...items, stream]);
    });
  }

  orderUpdated(event: IndexUpdateEvent) {
    this.sideNavService.updateSideNavStreamPosition(event.item.name, event.item.id, event.fromPosition, event.toPosition).subscribe(() => {
      this.sideNavService.getSideNavStreams(false).subscribe(data => this.items.set([...data]));
    });
  }

  updateVisibility(item: SideNavStream) {
    const updated = {...item, visible: !item.visible};
    this.items.update(items => items.map(s => s.id === item.id ? updated : s));
    this.sideNavService.updateSideNavStream(updated).subscribe();
  }

  delete(item: SideNavStream) {
    this.sideNavService.deleteSideNavSmartFilter(item.id).subscribe({
      next: () => {
        this.items.update(items => items.filter(i => i.id !== item.id));
      },
      error: err => {
        console.error(err);
      }
    })
  }

}
