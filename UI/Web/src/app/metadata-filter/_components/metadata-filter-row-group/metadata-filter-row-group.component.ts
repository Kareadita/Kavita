import { Component, Input } from '@angular/core';
import { FilterGroup } from 'src/app/_models/metadata/v2/filter-group';

@Component({
  selector: 'app-metadata-filter-row-group',
  templateUrl: './metadata-filter-row-group.component.html',
  styleUrls: ['./metadata-filter-row-group.component.scss']
})
export class MetadataFilterRowGroupComponent {

  @Input() filterGroup!: FilterGroup;

  

}
