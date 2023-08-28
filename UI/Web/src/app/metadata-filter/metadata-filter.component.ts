import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ContentChild,
  DestroyRef,
  EventEmitter,
  inject,
  Input,
  OnInit,
  Output
} from '@angular/core';
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule} from '@angular/forms';
import {NgbCollapse, NgbRating, NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {FilterUtilitiesService} from '../shared/_services/filter-utilities.service';
import {Breakpoint, UtilityService} from '../shared/_services/utility.service';
import {Library} from '../_models/library';
import {allSortFields, FilterEvent, FilterItem, SortField} from '../_models/metadata/series-filter';
import {ToggleService} from '../_services/toggle.service';
import {FilterSettings} from './filter-settings';
import {SeriesFilterV2} from '../_models/metadata/v2/series-filter-v2';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {TypeaheadComponent} from '../typeahead/_components/typeahead.component';
import {DrawerComponent} from '../shared/drawer/drawer.component';
import {AsyncPipe, NgForOf, NgIf, NgTemplateOutlet} from '@angular/common';
import {TranslocoModule} from "@ngneat/transloco";
import {SortFieldPipe} from "../pipe/sort-field.pipe";
import {MetadataBuilderComponent} from "./_components/metadata-builder/metadata-builder.component";
import {allFields} from "../_models/metadata/v2/filter-field";

@Component({
    selector: 'app-metadata-filter',
    templateUrl: './metadata-filter.component.html',
    styleUrls: ['./metadata-filter.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [NgIf, NgbCollapse, NgTemplateOutlet, DrawerComponent, NgbTooltip, TypeaheadComponent,
    ReactiveFormsModule, FormsModule, NgbRating, AsyncPipe, TranslocoModule, SortFieldPipe, MetadataBuilderComponent, NgForOf]
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
  public readonly utilityService = inject(UtilityService);


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
  allSortFields = allSortFields;
  allFilterFields = allFields;

  handleFilters(filter: SeriesFilterV2) {
    console.log('[metadata-filter] updating filter');
    this.filterV2 = filter;
  }


  private readonly cdRef = inject(ChangeDetectorRef);


  constructor(public toggleService: ToggleService) {}

  ngOnInit(): void {
    console.log('[metadata-filter] ngOnInit')
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

  deepClone(obj: any): any {
    if (obj === null || typeof obj !== 'object') {
      return obj;
    }

    if (obj instanceof Array) {
      return obj.map(item => this.deepClone(item));
    }

    const clonedObj: any = {};

    for (const key in obj) {
      if (Object.prototype.hasOwnProperty.call(obj, key)) {
        if (typeof obj[key] === 'object' && obj[key] !== null) {
          clonedObj[key] = this.deepClone(obj[key]);
        } else {
          clonedObj[key] = obj[key];
        }
      }
    }

    return clonedObj;
  }


  loadFromPresetsAndSetup() {
    this.fullyLoaded = false;

    console.log('[metadata-filter] loading from preset and setting up');
    this.filterV2 = this.deepClone(this.filterSettings.presetsV2);

    this.sortGroup = new FormGroup({
      sortField: new FormControl({value: this.filterV2?.sortOptions?.sortField || SortField.SortName, disabled: this.filterSettings.sortDisabled}, []),
      limitTo: new FormControl(this.filterV2?.limitTo || 0, [])
    });

    this.sortGroup.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      console.log('[metadata-filter] sortGroup value change');
      if (this.filterV2?.sortOptions === null) {
        this.filterV2.sortOptions = {
          isAscending: this.isAscendingSort,
          sortField: parseInt(this.sortGroup.get('sortField')?.value, 10)
        };
      }
      this.filterV2!.sortOptions!.sortField = parseInt(this.sortGroup.get('sortField')?.value, 10);
      this.filterV2!.limitTo = Math.max(parseInt(this.sortGroup.get('limitTo')?.value || '0', 10), 0);
      this.cdRef.markForCheck();
    });

    this.fullyLoaded = true;
    this.apply();

    //this.cdRef.markForCheck();
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
    console.log('[metadata-filter] updated filter sort order')
  }

  clear() {
    // Apply any presets which will trigger the "apply"
    this.loadFromPresetsAndSetup();
  }

  apply() {
    this.applyFilter.emit({isFirst: this.updateApplied === 0, filterV2: this.filterV2!});

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

    protected readonly Breakpoint = Breakpoint;
}
