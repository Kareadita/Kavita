import { ChangeDetectionStrategy, ChangeDetectorRef, Component, Input, OnDestroy, OnInit } from '@angular/core';
import { Subject, takeUntil } from 'rxjs';
import { Action, ActionItem } from 'src/app/_services/action-factory.service';
import { BulkSelectionService } from '../bulk-selection.service';

@Component({
  selector: 'app-bulk-operations',
  templateUrl: './bulk-operations.component.html',
  styleUrls: ['./bulk-operations.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BulkOperationsComponent implements OnInit, OnDestroy {

  @Input() actionCallback!: (action: ActionItem<any>, data: any) => void;

  topOffset: number = 56;
  hasMarkAsRead: boolean = false;
  hasMarkAsUnread: boolean = false;
  actions: Array<ActionItem<any>> = [];

  private onDestory: Subject<void> = new Subject();

  get Action() {
    return Action;
  }

  constructor(public bulkSelectionService: BulkSelectionService, private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit(): void {
    this.bulkSelectionService.actions$.pipe(takeUntil(this.onDestory)).subscribe(actions => {
      actions.forEach(a => a.callback = this.actionCallback.bind(this));
      this.actions = actions;
      this.hasMarkAsRead = this.actions.filter(act => act.action === Action.MarkAsRead).length > 0;
      this.hasMarkAsUnread = this.actions.filter(act => act.action === Action.MarkAsUnread).length > 0;
      this.cdRef.markForCheck();
    });
  }

  ngOnDestroy(): void {
    this.onDestory.next();
    this.onDestory.complete();
  }

  handleActionCallback(action: ActionItem<any>, data: any) {
    this.actionCallback(action, data);
  }

  performAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action, null);
    }
  }

  executeAction(action: Action) {
    const foundActions = this.actions.filter(act => act.action === action);
    if (foundActions.length > 0) {
      this.performAction(foundActions[0]);
    }
  }
}
