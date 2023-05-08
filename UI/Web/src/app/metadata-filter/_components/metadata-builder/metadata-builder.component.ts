import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { FilterStatement } from '../../../_models/metadata/v2/filter-statement';
import { Subject } from 'rxjs';
import { FilterComparison } from 'src/app/_models/metadata/v2/filter-comparison';
import { FilterField } from 'src/app/_models/metadata/v2/filter-field';
import { MetadataService } from 'src/app/_services/metadata.service';
import { FilterGroup } from 'src/app/_models/metadata/v2/filter-group';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';

@Component({
  selector: 'app-metadata-builder',
  templateUrl: './metadata-builder.component.html',
  styleUrls: ['./metadata-builder.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MetadataBuilderComponent implements OnInit {

  @Input() filterGroup!: FilterGroup;
  @Output() update: EventEmitter<FilterStatement[]> = new EventEmitter<FilterStatement[]>();

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly metadataService = inject(MetadataService);
  protected readonly utilityService = inject(UtilityService);
  private onDestroy: Subject<void> = new Subject();

  get Breakpoint() { return Breakpoint; }

  



  //filterStatements: Array<FilterStatement> = [this.metadataService.createDefaultFilterStatement()];

  

  
  
  // updateFilter(index: number, filterStmt: FilterStatement) {
  //   console.log('Filter at ', index, 'updated: ', filterStmt);
  //   this.metadataService.updateFilter(this.filterStatements, index, filterStmt);
  // }

  // addFilter(place: 'and' | 'or') {
  //   if (place === 'and') {
  //     this.filterStatements.push(this.metadataService.createDefaultFilterStatement());
  //   }
  // }

  // removeFilter(index: number) {
  //   this.filterStatements.splice(index, 1);
  // }

  ngOnInit() {
    console.log('Preset: ', this.filterGroup);

    if (!this.filterGroup) {
      this.filterGroup = this.metadataService.createDefaultFilterGroup();
      // const group = this.metadataService.createDefaultFilterGroup();
      this.filterGroup.statements.push(this.metadataService.createDefaultFilterStatement());
      this.filterGroup.id = 'root';
      //this.filterGroup.or.push(group);
    }

    console.log('Group: ', this.filterGroup);
  }

  updateFilterGroup(group: FilterGroup) {
    console.log('[builder] filter group update: ', group);
  }

}
