import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { MetadataService } from 'src/app/_services/metadata.service';
import { FilterGroup } from 'src/app/_models/metadata/v2/filter-group';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import { SeriesFilterV2 } from 'src/app/_models/metadata/v2/series-filter-v2';
import { SortField } from 'src/app/_models/metadata/series-filter';
import {ComparisonOption} from "../metadata-filter-row-group/metadata-filter-row-group.component";

@Component({
  selector: 'app-metadata-builder',
  templateUrl: './metadata-builder.component.html',
  styleUrls: ['./metadata-builder.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MetadataBuilderComponent implements OnInit {

  @Input() urlString: string = '';
  @Input({required: true}) filterGroup!: SeriesFilterV2;
  @Input() parentGroup!: FilterGroup;
  @Output() update: EventEmitter<SeriesFilterV2> = new EventEmitter<SeriesFilterV2>();

  groupPreset: ComparisonOption = ComparisonOption.OR;

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly metadataService = inject(MetadataService);
  protected readonly utilityService = inject(UtilityService);

  get Breakpoint() { return Breakpoint; }


  ngOnInit() {
    //console.log('Preset: ', this.filterGroup);

    // The big problem is that first level group isn't behaving like nested and thus the controls are slightly weird and building is too
    // we need a way to streamline or have the hack later down. This needs the DTO because this controls sort and limit as well
    if (!this.filterGroup) {
      console.log('builder had no preset')
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
      //this.update.emit(dto); // Do I need this here?
    }

    this.filterGroup.id = 'root';

    console.log('Group: ', this.filterGroup);
  }

  updateFilterGrouping(event: {group: ComparisonOption, filterGroup: FilterGroup}) {
    console.log('[updateFilterGroup] event: ', event);
    const group = event.group;
    const nestedGroup = event.filterGroup;
    if (group === 'and') {
      console.log('removing group from or -> and')
      this.filterGroup.or = this.filterGroup.or.filter(g => g !== nestedGroup);
      this.filterGroup.and.push(nestedGroup);
    } else if (group === 'or'){
      console.log('removing group from and -> or')
      this.filterGroup.and = this.filterGroup.and.filter(g => g !== nestedGroup);
      this.filterGroup.or.push(nestedGroup);
    }

    this.groupPreset = group;
    console.log('updated filterGroup: ', this.filterGroup);
    this.cdRef.markForCheck();
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
