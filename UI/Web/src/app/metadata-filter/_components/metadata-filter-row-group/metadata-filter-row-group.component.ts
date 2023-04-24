import { ChangeDetectionStrategy, ChangeDetectorRef, Component, Input, inject } from '@angular/core';
import { FilterGroup } from 'src/app/_models/metadata/v2/filter-group';
import { FilterStatement } from 'src/app/_models/metadata/v2/filter-statement';
import { MetadataService } from 'src/app/_services/metadata.service';

@Component({
  selector: 'app-metadata-filter-row-group',
  templateUrl: './metadata-filter-row-group.component.html',
  styleUrls: ['./metadata-filter-row-group.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MetadataFilterRowGroupComponent {

  @Input() filterGroup!: FilterGroup;

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly metadataService = inject(MetadataService);

  updateFilter(index: number, filterStmt: FilterStatement) {
    console.log('Filter at ', index, 'updated: ', filterStmt);
    this.metadataService.updateFilter(this.filterGroup.statements, index, filterStmt);
  }

  addFilter(place: 'and' | 'or') {
    if (place === 'and') {
      this.filterGroup.and.push(this.metadataService.createDefaultFilterGroup());
    } else {
      this.filterGroup.or.push(this.metadataService.createDefaultFilterGroup());
    }
  }

  removeFilter(index: number, group: FilterGroup) {
    group.statements.slice(index, 1);
    //this.filterStatements.splice(index, 1);
  }


}
