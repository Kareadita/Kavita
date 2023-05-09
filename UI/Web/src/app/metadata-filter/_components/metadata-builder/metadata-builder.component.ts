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
  @Output() update: EventEmitter<FilterGroup> = new EventEmitter<FilterGroup>();

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly metadataService = inject(MetadataService);
  protected readonly utilityService = inject(UtilityService);

  get Breakpoint() { return Breakpoint; }


  ngOnInit() {
    console.log('Preset: ', this.filterGroup);

    if (!this.filterGroup) {
      this.filterGroup = this.metadataService.createDefaultFilterGroup();
      // const group = this.metadataService.createDefaultFilterGroup();
      this.filterGroup.statements.push(this.metadataService.createDefaultFilterStatement());
      this.filterGroup.id = 'root';
      this.cdRef.markForCheck();
      this.update.emit(this.filterGroup);
    }

    console.log('Group: ', this.filterGroup);
  }

  updateFilterGroup(group: FilterGroup) {
    console.log('[builder] filter group update: ', group);

    this.update.emit(group);
    
  }

}
