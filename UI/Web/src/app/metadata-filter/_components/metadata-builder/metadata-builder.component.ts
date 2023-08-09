import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  EventEmitter,
  inject,
  Input,
  OnInit,
  Output
} from '@angular/core';
import {MetadataService} from 'src/app/_services/metadata.service';
import {Breakpoint, UtilityService} from 'src/app/shared/_services/utility.service';
import {SeriesFilterV2} from 'src/app/_models/metadata/v2/series-filter-v2';
import {MetadataFilterRowGroupComponent} from "../metadata-filter-row-group/metadata-filter-row-group.component";
import {NgForOf, NgIf, UpperCasePipe} from "@angular/common";
import {MetadataFilterRowComponent} from "../metadata-filter-row/metadata-filter-row.component";
import {FilterStatement} from "../../../_models/metadata/v2/filter-statement";
import {CardActionablesComponent} from "../../../_single-module/card-actionables/card-actionables.component";
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule} from "@angular/forms";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {FilterCombination} from "../../../_models/metadata/v2/filter-combination";
import {FilterUtilitiesService} from "../../../shared/_services/filter-utilities.service";
import {FilterComparison} from "../../../_models/metadata/v2/filter-comparison";
import {FilterField} from "../../../_models/metadata/v2/filter-field";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {take, tap} from "rxjs/operators";

@Component({
  selector: 'app-metadata-builder',
  templateUrl: './metadata-builder.component.html',
  styleUrls: ['./metadata-builder.component.scss'],
  standalone: true,
  imports: [
    MetadataFilterRowGroupComponent,
    NgIf,
    MetadataFilterRowComponent,
    NgForOf,
    CardActionablesComponent,
    FormsModule,
    NgbTooltip,
    UpperCasePipe,
    ReactiveFormsModule
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MetadataBuilderComponent implements OnInit {

  @Input({required: true}) filter!: SeriesFilterV2;
  @Output() update: EventEmitter<SeriesFilterV2> = new EventEmitter<SeriesFilterV2>();

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly metadataService = inject(MetadataService);
  protected readonly utilityService = inject(UtilityService);
  protected readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly  destroyRef = inject(DestroyRef);

  formGroup: FormGroup = new FormGroup({});

  groupOptions: Array<{value: FilterCombination, title: string}> = [
    {value: FilterCombination.Or, title: 'Match any of the following'},
    {value: FilterCombination.And, title: 'Match all of the following'},
  ];

  get Breakpoint() { return Breakpoint; }


  ngOnInit() {
    console.log('Filter: ', this.filter);
    if (this.filter === undefined) {
      // If there is no default preset, let's open with series name
      this.filter = this.filterUtilityService.createSeriesV2Filter();
      this.filter.statements.push({
        value: '',
        comparison: FilterComparison.Equal,
        field: FilterField.SeriesName
      });
    }

    this.formGroup.addControl('comparison', new FormControl<FilterCombination>(this.filter?.combination || FilterCombination.Or, []));
    this.formGroup.valueChanges.pipe(takeUntilDestroyed(this.destroyRef), tap(values => {
      this.filter.combination = parseInt(this.formGroup.get('comparison')?.value, 10);
      this.update.emit(this.filter);
    })).subscribe()
  }

  addFilter() {
    this.filter.statements = [this.metadataService.createDefaultFilterStatement(), ...this.filter.statements];
  }

  removeFilter(index: number) {
    this.filter.statements = this.filter.statements.slice(0, index).concat(this.filter.statements.slice(index + 1))
    this.cdRef.markForCheck();
  }

  updateFilter(index: number, filterStmt: FilterStatement) {
    console.log('Filter at ', index, 'updated: ', filterStmt);
    this.metadataService.updateFilter(this.filter.statements, index, filterStmt);
    this.update.emit(this.filter);
  }

}
