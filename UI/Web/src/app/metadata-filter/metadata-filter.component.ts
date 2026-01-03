import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  ContentChild,
  DestroyRef,
  effect,
  EventEmitter,
  inject,
  input,
  Input,
  OnInit,
  Output,
  Signal,
  TemplateRef
} from '@angular/core';
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule} from '@angular/forms';
import {NgbCollapse} from '@ng-bootstrap/ng-bootstrap';
import {Breakpoint, UtilityService} from '../shared/_services/utility.service';
import {Library} from '../_models/library/library';
import {FilterEvent, FilterItem} from '../_models/metadata/series-filter';
import {ToggleService} from '../_services/toggle.service';
import {FilterV2} from '../_models/metadata/v2/filter-v2';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {DrawerComponent} from '../shared/drawer/drawer.component';
import {AsyncPipe, NgClass, NgTemplateOutlet} from '@angular/common';
import {translate, TranslocoModule, TranslocoService} from "@jsverse/transloco";
import {MetadataBuilderComponent} from "./_components/metadata-builder/metadata-builder.component";
import {FilterService} from "../_services/filter.service";
import {ToastrService} from "ngx-toastr";
import {animate, style, transition, trigger} from "@angular/animations";
import {SortButtonComponent} from "../_single-module/sort-button/sort-button.component";
import {FilterSettingsBase} from "./filter-settings";
import {FilterUtilitiesService} from "../shared/_services/filter-utilities.service";


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


  private readonly destroyRef = inject(DestroyRef);
  public readonly utilityService = inject(UtilityService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly toastr = inject(ToastrService);
  private readonly filterService = inject(FilterService);
  protected readonly toggleService = inject(ToggleService);
  protected readonly translocoService = inject(TranslocoService);
  protected readonly filterUtilitiesService = inject(FilterUtilitiesService);

  /**
   * This toggles the opening/collapsing of the metadata filter code
   */
  @Input() filterOpen: EventEmitter<boolean> = new EventEmitter();

  filterSettings = input.required<FilterSettingsBase<TFilter, TSort>>();

  @Output() applyFilter: EventEmitter<FilterEvent<TFilter, TSort>> = new EventEmitter();
  @ContentChild('[ngbCollapse]') collapse!: NgbCollapse;

  /**
   * Template that is rendered next to the save button
   */
  @ContentChild('extraButtons') extraButtonsRef!: TemplateRef<any>;



   /**
   * Controls the visibility of extended controls that sit below the main header.
   */
  filteringCollapsed: boolean = true;
  libraries: Array<FilterItem<Library>> = [];

  sortGroup!: FormGroup;
  isAscendingSort: boolean = true;
  updateApplied: number = 0;

  fullyLoaded: boolean = false;
  filterV2: FilterV2<TFilter, TSort> | undefined;
  sortFieldOptions: Signal<{title: string, value: number}[]> = computed(() => []);
  filterFieldOptions: Signal<{title: string, value: number}[]> = computed(() => []);

  constructor() {
    effect(() => {
      const settings = this.filterSettings();
      if (settings?.presetsV2) {
        this.filterV2 = this.deepClone(settings.presetsV2);
        this.cdRef.markForCheck();
      }
    })
  }



  ngOnInit(): void {
    if (this.filterOpen) {
      this.filterOpen.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(openState => {
        this.filteringCollapsed = !openState;
        this.toggleService.set(!this.filteringCollapsed);
        this.cdRef.markForCheck();
      });
    }


    this.filterFieldOptions = computed(() => {
      return this.filterUtilitiesService.getFilterFields(this.filterSettings().type);
    });

    this.sortFieldOptions = computed(() => {
      return this.filterUtilitiesService.getSortFields(this.filterSettings().type);
    });



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

  handleFilters(filter: FilterV2<TFilter, TSort>) {
    this.filterV2 = filter;
  }


  loadFromPresetsAndSetup() {
    this.fullyLoaded = false;

    const currentFilterSettings = this.filterSettings();
    this.filterV2 = this.deepClone(currentFilterSettings.presetsV2);

    const defaultSortField = this.sortFieldOptions()[0].value;

    this.sortGroup = new FormGroup({
      sortField: new FormControl({value: this.filterV2?.sortOptions?.sortField || defaultSortField, disabled: this.filterSettings().sortDisabled}, []),
      limitTo: new FormControl(this.filterV2?.limitTo || 0, []),
      name: new FormControl(this.filterV2?.name || '', [])
    });

    if (this.filterSettings()?.presetsV2?.sortOptions) {
      this.isAscendingSort = this.filterSettings()?.presetsV2?.sortOptions!.isAscending || true;
    }

    this.cdRef.markForCheck();

    this.sortGroup.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      if (this.filterV2?.sortOptions === null) {
        this.filterV2.sortOptions = {
          isAscending: this.isAscendingSort,
          sortField: parseInt(this.sortGroup.get('sortField')?.value, 10) as TSort
        };
      }
      this.filterV2!.sortOptions!.sortField = parseInt(this.sortGroup.get('sortField')?.value, 10) as TSort;
      this.filterV2!.limitTo = Math.max(parseInt(this.sortGroup.get('limitTo')?.value || '0', 10), 0);
      this.filterV2!.name = this.sortGroup.get('name')?.value || '';
      this.cdRef.markForCheck();
    });

    this.fullyLoaded = true;
    this.apply();
  }


  updateSortOrder(isAscending: boolean) {
    if (this.filterSettings().sortDisabled) return;
    this.isAscendingSort = isAscending;

    if (this.filterV2?.sortOptions === null) {
      const defaultSortField = this.sortFieldOptions()[0].value as TSort;

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
    this.applyFilter.emit({isFirst: this.updateApplied === 0, filterV2: this.filterV2!} as FilterEvent<TFilter, TSort>);

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


  protected readonly Breakpoint = Breakpoint;
}
