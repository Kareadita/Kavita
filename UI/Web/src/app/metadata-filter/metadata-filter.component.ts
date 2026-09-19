import {
  ChangeDetectionStrategy,
  Component,
  computed,
  contentChild,
  DestroyRef,
  effect,
  EventEmitter,
  inject,
  input,
  linkedSignal,
  OnInit,
  output,
  signal,
  TemplateRef,
  untracked
} from '@angular/core';
import {UtilityService} from '../shared/_services/utility.service';
import {FilterEvent} from '../_models/metadata/series-filter';
import {ToggleService} from '../_services/toggle.service';
import {FilterV2} from '../_models/metadata/v2/filter-v2';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {DrawerComponent} from '../shared/drawer/drawer.component';
import {NgTemplateOutlet} from '@angular/common';
import {translate, TranslocoModule, TranslocoService} from "@jsverse/transloco";
import {MetadataBuilderComponent} from "./_components/metadata-builder/metadata-builder.component";
import {FilterService} from "../_services/filter.service";
import {ToastrService} from '@openng/ngx-toastr';
import {SortButtonComponent} from "../_single-module/sort-button/sort-button.component";
import {FilterSettingsBase} from "./filter-settings";
import {FilterUtilitiesService} from "../shared/_services/filter-utilities.service";
import {BreakpointService} from "../_services/breakpoint.service";
import {disabled, form, FormField, FormRoot} from "@angular/forms/signals";
import {SettingSelectComponent} from "../settings/_components/setting-enum-select/setting-select.component";
import {SortFieldPipe} from "../_pipes/sort-field.pipe";

interface FormModel {
  sortField: number;
  limitTo: number;
  name: string;
}


@Component({
  selector: 'app-metadata-filter',
  templateUrl: './metadata-filter.component.html',
  styleUrls: ['./metadata-filter.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgTemplateOutlet, DrawerComponent,
    TranslocoModule, MetadataBuilderComponent, SortButtonComponent,
    FormRoot, FormField, SettingSelectComponent, SortFieldPipe]
})
export class MetadataFilterComponent<TFilter extends number = number, TSort extends number = number> implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  public readonly utilityService = inject(UtilityService);
  private readonly toastr = inject(ToastrService);
  private readonly filterService = inject(FilterService);
  protected readonly toggleService = inject(ToggleService);
  protected readonly translocoService = inject(TranslocoService);
  protected readonly filterUtilitiesService = inject(FilterUtilitiesService);
  protected readonly breakpointService = inject(BreakpointService);

  /**
   * This toggles the opening/collapsing of the metadata filter code
   */
  readonly filterOpen = input<EventEmitter<boolean>>();

  filterSettings = input.required<FilterSettingsBase<TFilter, TSort>>();

  readonly applyFilter = output<FilterEvent<TFilter, TSort>>();

  /**
   * Template that is rendered next to the save button
   */
  readonly extraButtonsRef = contentChild.required<TemplateRef<any>>('extraButtons');

  private readonly formModel = signal<FormModel>({
    sortField: 0,
    limitTo: 0,
    name: ''
  });
  formGroup = form(this.formModel, p => {
    disabled(p.sortField, {when: ({valueOf}) => this.filterSettings().sortDisabled});
  });

  isSaveDisabled = computed(() => {
    return this.filterSettings().saveDisabled || !this.formGroup.name().value();
  })
  isAscendingSort = signal(true);
  updateApplied: number = 0;

  fullyLoaded = signal(false);
  /**
   * Working copy of the filter. Re-cloned when new presets come in, otherwise keeps the in-progress edits.
   */
  readonly filterV2 = linkedSignal<FilterSettingsBase<TFilter, TSort>, FilterV2<TFilter, TSort> | undefined>({
    source: this.filterSettings,
    computation: (settings, previous) => settings.presetsV2 ? this.deepClone(settings.presetsV2) : previous?.value
  });
  readonly sortFieldOptions = computed(() => {
    return this.filterUtilitiesService.getSortFields(this.filterSettings().type).map(t => t.value);
  });

  constructor() {
    effect(() => {
      const formData = this.formModel();
      untracked(() => this.packData(formData));
    });
  }



  ngOnInit(): void {
    this.filterOpen()?.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(openState => {
      this.toggleService.set(openState);
    });

    this.loadFromPresetsAndSetup();
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
    this.fullyLoaded.set(false);

    const currentFilterSettings = this.filterSettings();
    const filter: FilterV2<TFilter, TSort> | undefined = this.deepClone(currentFilterSettings.presetsV2);
    this.filterV2.set(filter);

    const defaultSortField = this.sortFieldOptions()[0];

    this.formModel.set({
      sortField: filter?.sortOptions?.sortField || defaultSortField,
      limitTo: filter?.limitTo || 0,
      name: filter?.name || ''
    });


    if (this.filterSettings()?.presetsV2?.sortOptions) {
      this.isAscendingSort.set(this.filterSettings()?.presetsV2?.sortOptions!.isAscending || true);
    }

    this.fullyLoaded.set(true);
    this.apply();
  }

  private packData(formData: FormModel) {
    this.filterV2.update(filter => {
      if (!filter) return filter;

      return {
        ...filter,
        sortOptions: {
          isAscending: filter.sortOptions?.isAscending ?? this.isAscendingSort(),
          sortField: formData.sortField as TSort
        },
        limitTo: Math.max(parseInt(formData.limitTo + '' || '0', 10), 0),
        name: formData.name || ''
      };
    });
  }


  updateSortOrder(isAscending: boolean) {
    if (this.filterSettings().sortDisabled) return;
    this.isAscendingSort.set(isAscending);

    this.filterV2.update(filter => {
      if (!filter) return filter;

      return {
        ...filter,
        sortOptions: {
          isAscending,
          sortField: filter.sortOptions?.sortField ?? this.sortFieldOptions()[0] as TSort
        }
      };
    });
  }

  clear() {
    // Apply any presets which will trigger the "apply"
    this.loadFromPresetsAndSetup();
  }

  apply() {
    this.applyFilter.emit({isFirst: this.updateApplied === 0, filterV2: this.filterV2()!} as FilterEvent<TFilter, TSort>);

    if (this.breakpointService.isMobile() && this.updateApplied !== 0) {
      this.toggleSelected();
    }

    this.updateApplied++;
  }

  save() {
    const filter = this.filterV2();
    if (!filter) return;

    const filterToSave = {...filter, name: this.formModel().name};
    this.filterV2.set(filterToSave);
    this.filterService.saveFilter(filterToSave).subscribe(() => {
      this.toastr.success(translate('toasts.smart-filter-updated'));
      this.apply();
    });
  }

  toggleSelected() {
    this.toggleService.toggle();
  }
}
