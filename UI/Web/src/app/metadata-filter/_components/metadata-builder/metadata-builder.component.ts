import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { MetadataService } from 'src/app/_services/metadata.service';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import { SeriesFilterV2 } from 'src/app/_models/metadata/v2/series-filter-v2';
import {
  ComparisonOption,
  MetadataFilterRowGroupComponent
} from "../metadata-filter-row-group/metadata-filter-row-group.component";
import {NgForOf, NgIf, UpperCasePipe} from "@angular/common";
import {MetadataFilterRowComponent} from "../metadata-filter-row/metadata-filter-row.component";
import {FilterStatement} from "../../../_models/metadata/v2/filter-statement";
import {CardActionablesComponent} from "../../../_single-module/card-actionables/card-actionables.component";
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule} from "@angular/forms";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {FilterCombination} from "../../../_models/metadata/v2/filter-combination";

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

  @Input() urlString: string = '';
  @Input({required: true}) filter!: SeriesFilterV2;
  @Output() update: EventEmitter<SeriesFilterV2> = new EventEmitter<SeriesFilterV2>();

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly metadataService = inject(MetadataService);
  protected readonly utilityService = inject(UtilityService);

  formGroup: FormGroup = new FormGroup({
    'comparison': new FormControl<FilterCombination>(this.filter.combination, [])
  });

  groupOptions: Array<{value: ComparisonOption, title: string}> = [
    {value: ComparisonOption.OR, title: 'Match any of the following'},
    {value: ComparisonOption.AND, title: 'Match all of the following'},
  ];

  get Breakpoint() { return Breakpoint; }


  ngOnInit() {
    console.log('Filter: ', this.filter);
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
