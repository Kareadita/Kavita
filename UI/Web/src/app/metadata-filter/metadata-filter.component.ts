import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ContentChild, DestroyRef,
  EventEmitter,
  inject,
  Input,
  OnInit,
  Output
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { NgbCollapse, NgbTooltip, NgbRating } from '@ng-bootstrap/ng-bootstrap';
import { FilterUtilitiesService } from '../shared/_services/filter-utilities.service';
import { Breakpoint, UtilityService } from '../shared/_services/utility.service';
import { Library } from '../_models/library';
import { FilterEvent, FilterItem, SortField } from '../_models/metadata/series-filter';
import { ToggleService } from '../_services/toggle.service';
import { FilterSettings } from './filter-settings';
import { SeriesFilterV2 } from '../_models/metadata/v2/series-filter-v2';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { TypeaheadComponent } from '../typeahead/_components/typeahead.component';
import { DrawerComponent } from '../shared/drawer/drawer.component';
import { NgIf, NgTemplateOutlet, AsyncPipe } from '@angular/common';
import {TranslocoModule} from "@ngneat/transloco";
import {SortFieldPipe} from "../pipe/sort-field.pipe";
import {MetadataBuilderComponent} from "./_components/metadata-builder/metadata-builder.component";

@Component({
    selector: 'app-metadata-filter',
    templateUrl: './metadata-filter.component.html',
    styleUrls: ['./metadata-filter.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [NgIf, NgbCollapse, NgTemplateOutlet, DrawerComponent, NgbTooltip, TypeaheadComponent,
    ReactiveFormsModule, FormsModule, NgbRating, AsyncPipe, TranslocoModule, SortFieldPipe, MetadataBuilderComponent]
})
export class MetadataFilterComponent implements OnInit {

  /**
   * This toggles the opening/collapsing of the metadata filter code
   */
  @Input() filterOpen: EventEmitter<boolean> = new EventEmitter();

  /**
   * Should filtering be shown on the page
   */
  @Input() filteringDisabled: boolean = false;

  @Input({required: true}) filterSettings!: FilterSettings;

  @Output() applyFilter: EventEmitter<FilterEvent> = new EventEmitter();

  @ContentChild('[ngbCollapse]') collapse!: NgbCollapse;
  private readonly destroyRef = inject(DestroyRef);


   /**
   * Controls the visibility of extended controls that sit below the main header.
   */
  filteringCollapsed: boolean = true;

  libraries: Array<FilterItem<Library>> = [];

  sortGroup!: FormGroup;
  isAscendingSort: boolean = true;

  updateApplied: number = 0;

  fullyLoaded: boolean = false;
  filterV2: SeriesFilterV2 | undefined;



  handleFilters(filter: SeriesFilterV2) {
    console.log('[metadata-filter] handleFilters');
    this.filterV2 = filter;
  }


  private readonly cdRef = inject(ChangeDetectorRef);


  get SortField(): typeof SortField {
    return SortField;
  }

  constructor(private utilityService: UtilityService,
    public toggleService: ToggleService,
    private filterUtilityService: FilterUtilitiesService) {
  }

  ngOnInit(): void {
    if (this.filterSettings === undefined) {
      this.filterSettings = new FilterSettings();
      this.cdRef.markForCheck();
    }

    if (this.filterOpen) {
      this.filterOpen.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(openState => {
        this.filteringCollapsed = !openState;
        this.toggleService.set(!this.filteringCollapsed);
        this.cdRef.markForCheck();
      });
    }

    this.loadFromPresetsAndSetup();
  }

  close() {
    this.filterOpen.emit(false);
    this.filteringCollapsed = true;
    this.toggleService.set(!this.filteringCollapsed);
    this.cdRef.markForCheck();
  }


  loadFromPresetsAndSetup() {
    this.fullyLoaded = false;

    this.filterV2 = this.filterSettings.presetsV2;
    console.log('filterV2: ', this.filterV2);

    this.sortGroup = new FormGroup({
      sortField: new FormControl({value: this.filterV2?.sortOptions?.sortField || SortField.SortName, disabled: this.filterSettings.sortDisabled}, []),
    });

    this.sortGroup.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(changes => {
      if (this.filterV2?.sortOptions === null) {
        this.filterV2.sortOptions = {
          isAscending: this.isAscendingSort,
          sortField: parseInt(this.sortGroup.get('sortField')?.value, 10)
        };
      }
      this.filterV2!.sortOptions!.sortField = parseInt(this.sortGroup.get('sortField')?.value, 10);
      this.cdRef.markForCheck();
    });

    this.fullyLoaded = true;
    this.cdRef.markForCheck();
    this.apply();
  }


  updateSortOrder() {
    if (this.filterSettings.sortDisabled) return;
    this.isAscendingSort = !this.isAscendingSort;
    if (this.filterV2?.sortOptions === null) {
      this.filterV2.sortOptions = {
        isAscending: this.isAscendingSort,
        sortField: SortField.SortName
      }
    }

    this.filterV2!.sortOptions!.isAscending = this.isAscendingSort;
  }

  clear() {
    this.filterV2 = this.filterUtilityService.createSeriesV2Filter();

    this.sortGroup.get('sortField')?.setValue(this.filterV2.sortOptions?.sortField);
    this.isAscendingSort = this.filterV2.sortOptions?.isAscending!;
    this.cdRef.markForCheck();
    // Apply any presets which will trigger the "apply"
    this.loadFromPresetsAndSetup();
  }

  apply() {

    this.applyFilter.emit({filter: this.filterUtilityService.createSeriesFilter(), isFirst: this.updateApplied === 0, filterV2: this.filterV2!});

    if (this.utilityService.getActiveBreakpoint() === Breakpoint.Mobile && this.updateApplied !== 0) {
      this.toggleSelected();
    }

    this.updateApplied++;
    this.cdRef.markForCheck();
  }

  toggleSelected() {
    this.toggleService.toggle();
    this.cdRef.markForCheck();
  }

  setToggle(event: any) {
    this.toggleService.set(!this.filteringCollapsed);
  }

}
