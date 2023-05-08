import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { FilterGroup } from 'src/app/_models/metadata/v2/filter-group';
import { FilterStatement } from 'src/app/_models/metadata/v2/filter-statement';
import { Action, ActionFactoryService } from 'src/app/_services/action-factory.service';
import { ActionItem } from 'src/app/_services/action-factory.service';
import { MetadataService } from 'src/app/_services/metadata.service';

@Component({
  selector: 'app-metadata-filter-row-group',
  templateUrl: './metadata-filter-row-group.component.html',
  styleUrls: ['./metadata-filter-row-group.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MetadataFilterRowGroupComponent {

  @Input() filterGroup!: FilterGroup;
  @Input() groupPreset: 'and' | 'or' = 'or';
  @Input() nestedLevel: number = 0;

  @Output() filterGroupUpdate = new EventEmitter<FilterGroup>();

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly metadataService = inject(MetadataService);
  private readonly actionFactoryService = inject(ActionFactoryService);

  groupOptions: Array<{value: 'and' | 'or', title: string}> = [
    {value: 'or', title: 'Match any of the following'},
    {value: 'and', title: 'Match all of the following'},
  ];
  formGroup: FormGroup = new FormGroup({
    'comparison': new FormControl<'or' | 'and'>(this.groupPreset || this.groupOptions[0].value, [])
  });

  actions: Array<ActionItem<any>> = this.actionFactoryService.getMetadataFilterActions(this.handleAction.bind(this));

  performAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action, null);
    }
  }

  handleAction(action: ActionItem<any>, data: any) {
    switch (action.action) {
      case(Action.AddRuleGroup):
        this.addGroup();
        break;
      case(Action.RemoveRuleGroup):
        this.removeGroup();
        break;
      default:
        break;
    }
  }

  addGroup() {
    const group = this.metadataService.createDefaultFilterGroup();
    group.statements.push(this.metadataService.createDefaultFilterStatement());
    group.id = this.formGroup.get('comparison')?.value + '-' + this.nestedLevel + 1;

    if (this.formGroup.get('comparison')?.value === 'or') {
      this.filterGroup.or.push(group);
    } else {
      this.filterGroup.and.push(group);
    }
  }

  removeGroup() {
    // I'll need some context here
  }

  updateFilter(index: number, filterStmt: FilterStatement) {
    console.log('Filter at ', index, 'updated: ', filterStmt);
    this.metadataService.updateFilter(this.filterGroup.statements, index, filterStmt);
    this.filterGroupUpdate.emit(this.filterGroup);
  }

  nestedFilterGroupUpdate(filterGroup: FilterGroup) {
    console.log('nested filter group updated: ', this.filterGroup, filterGroup);
    this.filterGroupUpdate.emit(this.filterGroup);
    this.cdRef.markForCheck();
  }

  addFilter() {
    this.filterGroup.statements.push(this.metadataService.createDefaultFilterStatement());
  }

  removeFilter(index: number, group: FilterGroup) {
    group.statements.slice(index, 1);
  }


}
