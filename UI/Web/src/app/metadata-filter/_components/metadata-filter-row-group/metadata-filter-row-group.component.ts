import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, OnDestroy, OnInit, Output, inject } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { FilterGroup } from 'src/app/_models/metadata/v2/filter-group';
import { FilterStatement } from 'src/app/_models/metadata/v2/filter-statement';
import { Action, ActionFactoryService } from 'src/app/_services/action-factory.service';
import { ActionItem } from 'src/app/_services/action-factory.service';
import { MetadataService } from 'src/app/_services/metadata.service';

export type FilterGrouping = {group: 'and' | 'or', filterGroup: FilterGroup};

@Component({
  selector: 'app-metadata-filter-row-group',
  templateUrl: './metadata-filter-row-group.component.html',
  styleUrls: ['./metadata-filter-row-group.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MetadataFilterRowGroupComponent implements OnInit, OnDestroy {

  @Input() parentGroup: FilterGroup | undefined;
  @Input() filterGroup!: FilterGroup;
  @Input() groupPreset: 'and' | 'or' = 'or';
  @Input() nestedLevel: number = 0;

  @Output() filterGroupUpdate = new EventEmitter<FilterGroup>();
  @Output() filterGroupingUpdate = new EventEmitter<FilterGrouping>();

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly metadataService = inject(MetadataService);
  private readonly actionFactoryService = inject(ActionFactoryService);
  private onDestroy: Subject<void> = new Subject();

  groupOptions: Array<{value: 'and' | 'or', title: string}> = [
    {value: 'or', title: 'Match any of the following'},
    {value: 'and', title: 'Match all of the following'},
  ];
  formGroup: FormGroup = new FormGroup({
    'comparison': new FormControl<'or' | 'and'>(this.groupPreset || this.groupOptions[0].value, [])
  });

  actions: Array<ActionItem<any>> = this.actionFactoryService.getMetadataFilterActions(this.handleAction.bind(this));

  ngOnInit() {
    console.log('setup group', this.filterGroup.id)
    this.formGroup.get('comparison')?.valueChanges.pipe(takeUntil(this.onDestroy)).subscribe(val => {
      //if (this.parentGroup == undefined) return;
      console.log('comparison change')
      this.filterGroupingUpdate.emit({group: val, filterGroup: this.filterGroup});
      this.cdRef.detectChanges();
    });
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  updateFilterGrouping(event: FilterGrouping) {
    this.filterGroupingUpdate.emit({group: event.group, filterGroup: event.filterGroup});
    this.cdRef.detectChanges();
  }

  performAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action, null);
    }
  }

  handleAction(action: ActionItem<any>, data: any) {
    switch (action.action) {
      case(Action.AddRuleGroup):
        this.addGroup(action.title === 'Add Rule Group (AND)' ? 'and' : 'or');
        break;
      case(Action.RemoveRuleGroup):
        this.removeGroup();
        break;
      default:
        break;
    }
  }

  addGroup(groupType: 'and' | 'or') {
    const group = this.metadataService.createDefaultFilterGroup();
    group.statements.push(this.metadataService.createDefaultFilterStatement());
    group.id = this.formGroup.get('comparison')?.value + '-' + this.nestedLevel + 1;


    //if (this.formGroup.get('comparison')?.value === 'or') {
    if (groupType === 'or') {
      this.filterGroup.or.push(group);
    } else {
      this.filterGroup.and.push(group);
    }
  }

  removeGroup() {
    // I'll need some context here
    if (!this.parentGroup || this.parentGroup.id === 'root') return;

    console.log('trying to remove group:', this.filterGroup.id)
    console.log('current group has comparison: ', this.groupPreset)

    if (this.groupPreset === 'and') {
      this.parentGroup.and = this.parentGroup.and.filter(f => f.id !== this.filterGroup.id);
    } else {
      this.parentGroup.or = this.parentGroup.or.filter(f => f.id !== this.filterGroup.id);
    }
    this.cdRef.markForCheck();
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
    this.filterGroup.statements = [this.metadataService.createDefaultFilterStatement(), ...this.filterGroup.statements];
  }

  removeFilter(index: number, group: FilterGroup) {
    group.statements = group.statements.slice(0, index).concat(group.statements.slice(index + 1))
    this.cdRef.markForCheck();
  }


}
