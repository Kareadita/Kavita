import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  ContentChild,
  DestroyRef,
  EventEmitter,
  inject,
  input,
  Input,
  OnInit,
  Output
} from '@angular/core';
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule} from '@angular/forms';
import {NgbCollapse} from '@ng-bootstrap/ng-bootstrap';
import {Breakpoint, UtilityService} from '../shared/_services/utility.service';
import {Library} from '../_models/library/library';
import {allSeriesSortFields, FilterEvent, FilterItem} from '../_models/metadata/series-filter';
import {ToggleService} from '../_services/toggle.service';
import {FilterV2} from '../_models/metadata/v2/filter-v2';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {DrawerComponent} from '../shared/drawer/drawer.component';
import {AsyncPipe, NgClass, NgTemplateOutlet} from '@angular/common';
import {translate, TranslocoModule, TranslocoService} from "@jsverse/transloco";
import {SortFieldPipe} from "../_pipes/sort-field.pipe";
import {MetadataBuilderComponent} from "./_components/metadata-builder/metadata-builder.component";
import {allSeriesFilterFields, FilterField} from "../_models/metadata/v2/filter-field";
import {FilterService} from "../_services/filter.service";
import {ToastrService} from "ngx-toastr";
import {animate, style, transition, trigger} from "@angular/animations";
import {SortButtonComponent} from "../_single-module/sort-button/sort-button.component";
import {FilterUtilitiesService} from "../shared/_services/filter-utilities.service";
import {FilterSettingsBase} from "./filter-settings";
import {allPersonSortFields} from "../_models/metadata/v2/person-sort-field";
import {allPersonFilterFields} from "../_models/metadata/v2/person-filter-field";

@Component({
  selector: 'app-metadata-filter',
  templateUrl: './metadata-filter.component.html',
  styleUrls: ['./metadata-filter.component.scss'],
  animations: [
      trigger('inOutAnimation', [
          transition(':enter', [
              style({ height: 0, opacity: 0 }),
              animate('.5s ease-out', style({ height: 300, opacity: 1 }))
          ]),
          transition(':leave', [
              style({ height: 300, opacity: 1 }),
              animate('.5s ease-in', style({ height: 0, opacity: 0 }))
          ])
      ]),
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgTemplateOutlet, DrawerComponent,
    ReactiveFormsModule, FormsModule, AsyncPipe, TranslocoModule,
    MetadataBuilderComponent, NgClass, SortButtonComponent]
})
export class MetadataFilterComponent<TFilter extends number = number, TSort extends number = number> implements OnInit {
  protected readonly allFilterFields = allSeriesFilterFields;

  private readonly destroyRef = inject(DestroyRef);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  public readonly utilityService = inject(UtilityService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly toastr = inject(ToastrService);
  private readonly filterService = inject(FilterService);
  protected readonly toggleService = inject(ToggleService);
  protected readonly translocoService = inject(TranslocoService);
  private readonly sortFieldPipe = new SortFieldPipe(this.translocoService);

  protected readonly allSortFields = allSeriesSortFields.map(f => {
    return {title: this.sortFieldPipe.transform(f), value: f};
  }).sort((a, b) => a.title.localeCompare(b.title));


  /**
   * This toggles the opening/collapsing of the metadata filter code
   */
  @Input() filterOpen: EventEmitter<boolean> = new EventEmitter();
  /**
   * Should filtering be shown on the page
   */
  @Input() filteringDisabled: boolean = false;
  //@Input({required: true}) filterSettings!: FilterSettings<T>;

  @Input({required: true}) filterSettings!: FilterSettingsBase<TFilter, TSort>;
  /**
   * Entity type derives the Sort and Filter fields
   */
  entityType = input<'series' | 'person'>('series');

  sortFieldOptions = computed(() => {
    if (this.entityType() === 'series') return allSeriesSortFields;
    return allPersonSortFields;
  });
  filterFieldOptions = computed(() => {
    if (this.entityType() === 'series') return allSeriesFilterFields;
    return allPersonFilterFields;
  });

  @Output() applyFilter: EventEmitter<FilterEvent> = new EventEmitter();
  @ContentChild('[ngbCollapse]') collapse!: NgbCollapse;



   /**
   * Controls the visibility of extended controls that sit below the main header.
   */
  filteringCollapsed: boolean = true;
  libraries: Array<FilterItem<Library>> = [];

  sortGroup!: FormGroup;
  isAscendingSort: boolean = true;
  updateApplied: number = 0;

  fullyLoaded: boolean = false;
  filterV2: FilterV2<FilterField> | undefined;




  ngOnInit(): void {
    if (this.filterSettings === undefined) {
      this.filterSettings = new FilterSettingsBase<TFilter, TSort>();
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

  handleFilters(filter: FilterV2<FilterField>) {
    this.filterV2 = filter;
  }


  loadFromPresetsAndSetup() {
    this.fullyLoaded = false;

    this.filterV2 = this.deepClone(this.filterSettings.presetsV2);

    const defaultSortField = this.sortFieldOptions()[0];

    this.sortGroup = new FormGroup({
      sortField: new FormControl({value: this.filterV2?.sortOptions?.sortField || defaultSortField, disabled: this.filterSettings.sortDisabled}, []),
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


  updateSortOrder(isAscending: boolean) {
    if (this.filterSettings.sortDisabled) return;
    this.isAscendingSort = isAscending;

    if (this.filterV2?.sortOptions === null) {
      const defaultSortField = this.sortFieldOptions()[0];

      this.filterV2.sortOptions = {
        isAscending: this.isAscendingSort,
        sortField: defaultSortField
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
