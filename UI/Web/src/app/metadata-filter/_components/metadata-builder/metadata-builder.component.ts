import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { FilterStatement } from '../../../_models/metadata/v2/filter-statement';
import { Subject } from 'rxjs';
import { FilterComparison } from 'src/app/_models/metadata/v2/filter-comparison';
import { FilterField } from 'src/app/_models/metadata/v2/filter-field';
import { MetadataService } from 'src/app/_services/metadata.service';
import { FilterGroup } from 'src/app/_models/metadata/v2/filter-group';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import { SeriesFilterV2 } from 'src/app/_models/metadata/v2/series-filter-v2';
import { SortField } from 'src/app/_models/metadata/series-filter';

@Component({
  selector: 'app-metadata-builder',
  templateUrl: './metadata-builder.component.html',
  styleUrls: ['./metadata-builder.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MetadataBuilderComponent implements OnInit {

  @Input() filterGroup!: FilterGroup;
  @Output() update: EventEmitter<SeriesFilterV2> = new EventEmitter<SeriesFilterV2>();

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly metadataService = inject(MetadataService);
  protected readonly utilityService = inject(UtilityService);

  get Breakpoint() { return Breakpoint; }


  ngOnInit() {
    console.log('Preset: ', this.filterGroup);

    if (!this.filterGroup) {
      this.filterGroup = this.metadataService.createDefaultFilterGroup();
      this.filterGroup.statements.push(this.metadataService.createDefaultFilterStatement());
      this.cdRef.markForCheck();
      const dto: SeriesFilterV2 = {
        groups: [this.filterGroup],
        limitTo: 0,
        sortOptions: {
          isAscending: true,
          sortField: SortField.SortName
        }
      };
      
      this.update.emit(dto);
    }

    this.filterGroup.id = 'root';

    console.log('Group: ', this.filterGroup);
  }

  updateFilterGroup(group: FilterGroup) {
    console.log('[builder] filter group update: ', group);

    const dto: SeriesFilterV2 = {
      groups: [group],
      limitTo: 0,
      sortOptions: {
        isAscending: true,
        sortField: SortField.SortName
      }
    };

    this.update.emit(dto);
    
  }

}
