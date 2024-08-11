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
import {NgbCollapse, NgbModal, NgbRating, NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {Breakpoint, UtilityService} from '../shared/_services/utility.service';
import {Library} from '../_models/library/library';
import {allSortFields, FilterEvent, FilterItem, SortField} from '../_models/metadata/series-filter';
import {ToggleService} from '../_services/toggle.service';
import {FilterSettings} from './filter-settings';
import {SeriesFilterV2} from '../_models/metadata/v2/series-filter-v2';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {TypeaheadComponent} from '../typeahead/_components/typeahead.component';
import {DrawerComponent} from '../shared/drawer/drawer.component';
import {AsyncPipe, NgClass, NgForOf, NgIf, NgTemplateOutlet} from '@angular/common';
import {translate, TranslocoModule} from "@jsverse/transloco";
import {SortFieldPipe} from "../_pipes/sort-field.pipe";
import {MetadataBuilderComponent} from "./_components/metadata-builder/metadata-builder.component";
import {allFields} from "../_models/metadata/v2/filter-field";
import {MetadataService} from "../_services/metadata.service";
import {FilterUtilitiesService} from "../shared/_services/filter-utilities.service";
import {FilterService} from "../_services/filter.service";
import {ToastrService} from "ngx-toastr";
import {
  Select2AutoCreateEvent,
  Select2Module,
  Select2Option,
  Select2UpdateEvent,
  Select2UpdateValue
} from "ng-select2-component";
import {SmartFilter} from "../_models/metadata/v2/smart-filter";
import {animate, state, style, transition, trigger} from "@angular/animations";

const ANIMATION_SPEED = 750;

@Component({
  selector: 'app-metadata-filter',
  templateUrl: './metadata-filter.component.html',
  styleUrls: ['./metadata-filter.component.scss'],
  animations: [
    trigger(
      'inOutAnimation',
      [
        transition(
          ':enter',
          [
            style({ height: 0, opacity: 0 }),
            animate('.5s ease-out',
              style({ height: 300, opacity: 1 }))
          ]
        ),
        transition(
          ':leave',
          [
            style({ height: 300, opacity: 1 }),
            animate('.5s ease-in',
              style({ height: 0, opacity: 0 }))
          ]
        )
      ]
    ),
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [NgIf, NgbCollapse, NgTemplateOutlet, DrawerComponent, NgbTooltip, TypeaheadComponent,
    ReactiveFormsModule, FormsModule, NgbRating, AsyncPipe, TranslocoModule, SortFieldPipe,
    MetadataBuilderComponent, NgForOf, Select2Module, NgClass]
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
  public readonly filterUtilitiesService = inject(FilterUtilitiesService);


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

  smartFilters!: Array<Select2Option>;

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly toastr = inject(ToastrService);


  constructor(public toggleService: ToggleService, private filterService: FilterService) {
    this.filterService.getAllFilters().subscribe(res => {
      this.smartFilters = res.map(r => {
        return {
          value: r,
          label: r.name,
        }
      });
    });
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

  // loadSavedFilter(event: Select2UpdateEvent<any>) {
  //   // Load the filter from the backend and update the screen
  //   if (event.value === undefined || typeof(event.value) === 'string') return;
  //   const smartFilter = event.value as SmartFilter;
  //   this.filterV2 = this.filterUtilitiesService.decodeSeriesFilter(smartFilter.filter);
  //   this.cdRef.markForCheck();
  //   console.log('update event: ', event);
  // }
  //
  // createFilterValue(event: Select2AutoCreateEvent<any>) {
  //   // Create a new name and filter
  //   if (!this.filterV2) return;
  //   this.filterV2.name = event.value;
  //   this.filterService.saveFilter(this.filterV2).subscribe(() => {
  //
  //     const item = {
  //       value: {
  //         filter: this.filterUtilitiesService.encodeSeriesFilter(this.filterV2!),
  //         name: event.value,
  //       } as SmartFilter,
  //       label: event.value
  //     };
  //     this.smartFilters.push(item);
  //     this.sortGroup.get('name')?.setValue(item);
  //     this.cdRef.markForCheck();
  //     this.toastr.success(translate('toasts.smart-filter-updated'));
  //     this.apply();
  //   });
  //
  //   console.log('create event: ', event);
  // }


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

  handleFilters(filter: SeriesFilterV2) {
    this.filterV2 = filter;
  }


  loadFromPresetsAndSetup() {
    this.fullyLoaded = false;

    this.filterV2 = this.deepClone(this.filterSettings.presetsV2);

    this.sortGroup = new FormGroup({
      sortField: new FormControl({value: this.filterV2?.sortOptions?.sortField || SortField.SortName, disabled: this.filterSettings.sortDisabled}, []),
      limitTo: new FormControl(this.filterV2?.limitTo || 0, []),
      name: new FormControl(this.filterV2?.name || '', [])
    });
    if (this.filterSettings?.presetsV2?.sortOptions) {
      this.isAscendingSort = this.filterSettings?.presetsV2?.sortOptions!.isAscending;
    }


    this.sortGroup.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
    if (this.filterV2?.sortOptions === null) {
      this.filterV2.sortOptions = {
        isAscending: this.isAscendingSort,
        sortField: parseInt(this.sortGroup.get('sortField')?.value, 10)
      };
    }
    this.filterV2!.sortOptions!.sortField = parseInt(this.sortGroup.get('sortField')?.value, 10);
    this.filterV2!.limitTo = Math.max(parseInt(this.sortGroup.get('limitTo')?.value || '0', 10), 0);
    this.filterV2!.name = this.sortGroup.get('name')?.value || '';
    this.cdRef.markForCheck();
    });

    this.fullyLoaded = true;
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
    this.cdRef.markForCheck();
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

  save() {
    if (!this.filterV2) return;
    this.filterV2.name = this.sortGroup.get('name')?.value;
    this.filterService.saveFilter(this.filterV2).subscribe(() => {
      this.toastr.success(translate('toasts.smart-filter-updated'));
      this.apply();
    });
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
