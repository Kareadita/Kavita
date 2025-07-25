import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  EventEmitter,
  inject,
  input,
  Input,
  OnInit,
  Output
} from '@angular/core';
import {MetadataService} from 'src/app/_services/metadata.service';
import {Breakpoint, UtilityService} from 'src/app/shared/_services/utility.service';
import {FilterV2} from 'src/app/_models/metadata/v2/filter-v2';
import {MetadataFilterRowComponent} from "../metadata-filter-row/metadata-filter-row.component";
import {FilterStatement} from "../../../_models/metadata/v2/filter-statement";
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule} from "@angular/forms";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {FilterCombination} from "../../../_models/metadata/v2/filter-combination";
import {FilterUtilitiesService} from "../../../shared/_services/filter-utilities.service";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {distinctUntilChanged, tap} from "rxjs/operators";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ValidFilterEntity} from "../../filter-settings";

@Component({
  selector: 'app-metadata-builder',
  templateUrl: './metadata-builder.component.html',
  styleUrls: ['./metadata-builder.component.scss'],
  imports: [
      MetadataFilterRowComponent,
      FormsModule,
      NgbTooltip,
      ReactiveFormsModule,
      TranslocoDirective
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MetadataBuilderComponent<TFilter extends number = number, TSort extends number = number> implements OnInit {

  @Input({required: true}) filter!: FilterV2<TFilter, TSort>;
  /**
   * The number of statements that can be. 0 means unlimited. -1 means none.
   */
  @Input() statementLimit = 0;
  entityType = input.required<ValidFilterEntity>();
  @Output() update: EventEmitter<FilterV2<TFilter, TSort>> = new EventEmitter<FilterV2<TFilter, TSort>>();
  @Output() apply: EventEmitter<void> = new EventEmitter<void>();

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly metadataService = inject(MetadataService);
  protected readonly utilityService = inject(UtilityService);
  protected readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly  destroyRef = inject(DestroyRef);

  formGroup: FormGroup = new FormGroup({});

  groupOptions: Array<{value: FilterCombination, title: string}> = [
    {value: FilterCombination.Or, title: translate('metadata-builder.or')},
    {value: FilterCombination.And, title: translate('metadata-builder.and')},
  ];

  ngOnInit() {

    this.formGroup.addControl('comparison', new FormControl<FilterCombination>(this.filter?.combination || FilterCombination.Or, []));

    this.formGroup.valueChanges.pipe(distinctUntilChanged(), takeUntilDestroyed(this.destroyRef), tap(values => {
      this.filter.combination = parseInt(this.formGroup.get('comparison')?.value, 10) as FilterCombination;
      this.update.emit(this.filter);
    })).subscribe();
  }

  addFilter() {
    const statement = this.metadataService.createFilterStatement<TFilter>(this.filterUtilityService.getDefaultFilterField(this.entityType()));
    this.filter.statements = [statement, ...this.filter.statements];
    this.cdRef.markForCheck();
  }

  removeFilter(index: number) {
    this.filter.statements = this.filter.statements.slice(0, index).concat(this.filter.statements.slice(index + 1))
    this.cdRef.markForCheck();
  }

  updateFilter(index: number, filterStmt: FilterStatement<number>) {
    this.metadataService.updateFilter(this.filter.statements, index, filterStmt);
    this.update.emit(this.filter);
  }

  protected readonly Breakpoint = Breakpoint;

}
