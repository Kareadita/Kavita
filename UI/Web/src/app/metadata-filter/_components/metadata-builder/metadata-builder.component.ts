import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, OnInit, Output, inject } from '@angular/core';
import { FilterStatement } from '../../../_models/metadata/v2/filter-statement';
import { Subject } from 'rxjs';
import { FilterComparison } from 'src/app/_models/metadata/v2/filter-comparison';
import { FilterField } from 'src/app/_models/metadata/v2/filter-field';
import { MetadataService } from 'src/app/_services/metadata.service';

@Component({
  selector: 'app-metadata-builder',
  templateUrl: './metadata-builder.component.html',
  styleUrls: ['./metadata-builder.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MetadataBuilderComponent implements OnInit {

  @Output() update: EventEmitter<FilterStatement[]> = new EventEmitter<FilterStatement[]>();

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly metadataService = inject(MetadataService);
  private onDestroy: Subject<void> = new Subject();



  filterStatements: Array<FilterStatement> = [this.metadataService.createDefaultFilterStatement()];

  groupOptions: Array<{value: 'and' | 'or', title: string}> = [
    {value: 'or', title: 'Match any of the following'},
    {value: 'and', title: 'Match all of the following'},
  ];

  
  
  updateFilter(index: number, filterStmt: FilterStatement) {
    console.log('Filter at ', index, 'updated: ', filterStmt);
    this.metadataService.updateFilter(this.filterStatements, index, filterStmt);
  }

  addFilter(place: 'and' | 'or') {
    if (place === 'and') {
      this.filterStatements.push(this.metadataService.createDefaultFilterStatement());
    }
  }

  removeFilter(index: number) {
    this.filterStatements.splice(index, 1);
  }

  ngOnInit() {
    console.log('Preset: ', this.filterStatements);
  }

}
