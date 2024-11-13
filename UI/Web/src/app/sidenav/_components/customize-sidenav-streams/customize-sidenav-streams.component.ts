import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  HostListener,
  inject,
  OnDestroy
} from '@angular/core';
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
import {TranslocoDirective} from "@jsverse/transloco";
import {SidenavStreamListItemComponent} from "../sidenav-stream-list-item/sidenav-stream-list-item.component";
import {ExternalSourceService} from "../../../_services/external-source.service";
import {ExternalSource} from "../../../_models/sidenav/external-source";
import {SideNavStreamType} from "../../../_models/sidenav/sidenav-stream-type.enum";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {FilterPipe} from "../../../_pipes/filter.pipe";
import {BulkOperationsComponent} from "../../../cards/bulk-operations/bulk-operations.component";
import {Action, ActionItem} from "../../../_services/action-factory.service";
import {BulkSelectionService} from "../../../cards/bulk-selection.service";
import {tap} from "rxjs/operators";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {Breakpoint, KEY_CODES, UtilityService} from "../../../shared/_services/utility.service";

@Component({
  selector: 'app-customize-sidenav-streams',
  standalone: true,
  imports: [DraggableOrderedListComponent, DashboardStreamListItemComponent, TranslocoDirective, SidenavStreamListItemComponent, ReactiveFormsModule, FilterPipe, BulkOperationsComponent],
  templateUrl: './customize-sidenav-streams.component.html',
  styleUrls: ['./customize-sidenav-streams.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CustomizeSidenavStreamsComponent implements OnDestroy {

  private readonly sideNavService = inject(NavService);
  private readonly filterService = inject(FilterService);
  private readonly externalSourceService = inject(ExternalSourceService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  public readonly bulkSelectionService = inject(BulkSelectionService);
  public readonly utilityService = inject(UtilityService);

  items: SideNavStream[] = [];
  smartFilters: SmartFilter[] = [];
  externalSources: ExternalSource[] = [];
  virtualizeAfter = 100;

  listForm: FormGroup = new FormGroup({
    'filterSideNavStream': new FormControl('', []),
    'filterSmartFilter': new FormControl('', []),
    'filterExternalSource': new FormControl('', []),
  });
  pageOperationsForm: FormGroup = new FormGroup({
    'accessibilityMode': new FormControl(false, []),
    'bulkMode': new FormControl(false, [])
  })

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

  bulkActionCallback = (action: ActionItem<SideNavStream>, data: SideNavStream) => {
    const selectedItems = this.bulkSelectionService.getSelectedCardsForSource('sideNavStream');
    const streams = selectedItems
        .map(index => this.items[parseInt(index, 10)]);
    let visibleState = false;
    switch (action.action) {
      case Action.MarkAsVisible:
        visibleState = true;
        break;
      case Action.MarkAsInvisible:
        visibleState = false;
        break;
    }

    for(let index of selectedItems.map(s => parseInt(s, 10))) {
      this.items[index].visible = visibleState;
      this.items[index] = {...this.items[index]};
    }
    this.cdRef.markForCheck();
    // Make bulk call
    this.sideNavService.bulkToggleSideNavStreamVisibility(streams.map(s => s.id), visibleState)
        .subscribe(() => {
          this.bulkSelectionService.deselectAll();
          this.cdRef.markForCheck();
        });
  }

  constructor() {

    this.pageOperationsForm.get('accessibilityMode')?.valueChanges.pipe(
        tap(_ => {
          const accessibleValue = this.pageOperationsForm.get('accessibilityMode')?.value;
          if (accessibleValue) {
            if (this.pageOperationsForm.get('bulkMode')?.disabled) return;
            this.pageOperationsForm.get('bulkMode')?.disable();
          } else {
            if (!this.pageOperationsForm.get('bulkMode')?.disabled) return;
            this.pageOperationsForm.get('bulkMode')?.enable();
          }
          this.cdRef.markForCheck();
        }),
        takeUntilDestroyed(this.destroyRef)
    ).subscribe();

    this.pageOperationsForm.get('bulkMode')?.valueChanges.pipe(
        tap(_ => {
          const bulkValue = this.pageOperationsForm.get('bulkMode')?.value;
          if (bulkValue) {
            if (this.pageOperationsForm.get('accessibilityMode')?.disabled) return;
            this.pageOperationsForm.get('accessibilityMode')?.disable();
          } else {
            this.pageOperationsForm.get('accessibilityMode')?.enable();
          }
        }),
        takeUntilDestroyed(this.destroyRef)
    ).subscribe();

    this.pageOperationsForm.valueChanges.pipe(
        tap(_ => {
          if (this.pageOperationsForm.value.accessibilityMode || this.pageOperationsForm.value.bulkMode) {
            this.listForm.get('filterSideNavStream')?.disable();
            return;
          }
          this.listForm.get('filterSideNavStream')?.enable();
        }),
        takeUntilDestroyed(this.destroyRef)
    ).subscribe();

    forkJoin([this.sideNavService.getSideNavStreams(false),
        this.filterService.getAllFilters(), this.externalSourceService.getExternalSources()
    ]).subscribe(results => {
      this.items = results[0];

      // After X items, drag and drop is disabled to use virtualization
      if (this.items.length > this.virtualizeAfter || this.utilityService.getActiveBreakpoint() <= Breakpoint.Tablet) {
        this.pageOperationsForm.get('accessibilityMode')?.setValue(true);
      }

      const existingSmartFilterStreams = new Set(results[0].filter(d => !d.isProvided && d.streamType === SideNavStreamType.SmartFilter).map(d => d.name));
      this.smartFilters = results[1].filter(d => !existingSmartFilterStreams.has(d.name));

      const existingExternalSourceStreams = new Set(results[0].filter(d => !d.isProvided && d.streamType === SideNavStreamType.ExternalSource).map(d => d.name));
      this.externalSources = results[2].filter(d => !existingExternalSourceStreams.has(d.name));
      this.cdRef.markForCheck();
    });
  }

  ngOnDestroy() {
    this.bulkSelectionService.deselectAll();
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


  orderUpdated(event: IndexUpdateEvent) {

    this.sideNavService.updateSideNavStreamPosition(event.item.name, event.item.id, event.fromPosition, event.toPosition).subscribe(() => {
      this.sideNavService.getSideNavStreams(false).subscribe((data) => {
        this.items = [...data];
        this.cdRef.markForCheck();
      });
    });
  }

  updateVisibility(item: SideNavStream, position: number) {
    const stream = this.items.filter(s => s.id == item.id)[0];
    stream.visible = !stream.visible;
    this.cdRef.markForCheck();
    this.sideNavService.updateSideNavStream(stream).subscribe();
  }

}
