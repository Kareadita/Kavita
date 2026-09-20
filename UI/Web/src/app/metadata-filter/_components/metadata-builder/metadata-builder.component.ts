import {ChangeDetectionStrategy, Component, DestroyRef, inject, input, linkedSignal, model} from '@angular/core';
import {MetadataFilterRowComponent} from "../metadata-filter-row/metadata-filter-row.component";
import {FilterStatement} from "../../../_models/metadata/v2/filter-statement";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {FilterCombination} from "../../../_models/metadata/v2/filter-combination";
import {FilterUtilitiesService} from "../../../shared/_services/filter-utilities.service";
import {TranslocoDirective} from "@jsverse/transloco";
import {ValidFilterEntity} from "../../filter-settings";
import {BreakpointService} from "../../../_services/breakpoint.service";
import {FilterV2} from "../../../_models/metadata/v2/filter-v2";
import {MetadataService} from "../../../_services/metadata.service";
import {form, FormField} from "@angular/forms/signals";
import {SettingSelectComponent} from "../../../settings/_components/setting-enum-select/setting-select.component";
import {FilterCombinationPipe} from "../../../_pipes/filter-combination.pipe";
import {takeUntilDestroyed, toObservable} from "@angular/core/rxjs-interop";
import {distinctUntilChanged, map} from "rxjs/operators";

interface FormModel {
  combination: FilterCombination;
}

@Component({
  selector: 'app-metadata-builder',
  templateUrl: './metadata-builder.component.html',
  styleUrls: ['./metadata-builder.component.scss'],
  imports: [
    MetadataFilterRowComponent,
    NgbTooltip,
    TranslocoDirective,
    SettingSelectComponent,
    FormField,
    FilterCombinationPipe
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MetadataBuilderComponent<TFilter extends number = number, TSort extends number = number> {

  filter = model.required<FilterV2<TFilter, TSort>>();
  /**
   * The number of statements that can be. 0 means unlimited. -1 means none.
   */
  statementLimit = input(0);
  entityType = input.required<ValidFilterEntity>();

  private readonly metadataService = inject(MetadataService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly filterUtilityService = inject(FilterUtilitiesService);
  protected readonly breakpointService = inject(BreakpointService);

  /**
   * Re-seeds whenever the filter is swapped out (ie presets being re-applied), so the select always mirrors the filter.
   */
  private readonly formModel = linkedSignal<FilterCombination, FormModel>({
    source: () => this.filter().combination,
    computation: (combination) => ({combination})
  });
  formGroup = form(this.formModel);

  protected readonly groupOptions = [FilterCombination.Or, FilterCombination.And];

  constructor() {
    toObservable(this.formModel).pipe(
      map(formValue => formValue.combination),
      distinctUntilChanged(),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(combination => this.updateCombination(combination));
  }

  addFilter() {
    const statement = this.metadataService.createFilterStatement<TFilter>(this.filterUtilityService.getDefaultFilterField(this.entityType()));
    this.filter.update(f => ({...f, statements: [statement, ...f.statements]}));
  }

  removeFilter(index: number) {
    this.filter.update(f => ({...f, statements: f.statements.slice(0, index).concat(f.statements.slice(index + 1))}));
  }

  updateFilter(index: number, filterStmt: FilterStatement<number>) {
    this.metadataService.updateFilter(this.filter().statements, index, filterStmt);
    this.filter.update(f => ({...f}));
  }

  private updateCombination(combination: FilterCombination) {
    if (this.filter().combination === combination) return;
    this.filter.update(f => ({...f, combination}));
  }

}
