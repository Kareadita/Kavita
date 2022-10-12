import { ChangeDetectionStrategy, ChangeDetectorRef, Component, Input, OnDestroy, OnInit } from '@angular/core';
import { Subject, takeUntil } from 'rxjs';
import { Action, ActionFactoryService, ActionItem } from 'src/app/_services/action-factory.service';
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

  constructor(public bulkSelectionService: BulkSelectionService, private readonly cdRef: ChangeDetectorRef,
    private actionFactoryService: ActionFactoryService) { }

  ngOnInit(): void {
    this.bulkSelectionService.actions$.pipe(takeUntil(this.onDestory)).subscribe(actions => {
      // We need to do a recursive callback apply
      this.actions = this.actionFactoryService.applyCallbackToList(actions, this.actionCallback.bind(this));
      this.hasMarkAsRead = this.actionFactoryService.hasAction(this.actions, Action.MarkAsRead);
      this.hasMarkAsUnread = this.actionFactoryService.hasAction(this.actions, Action.MarkAsUnread);
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
    this.actionCallback(action, null);
  }

  executeAction(action: Action) {
    const foundActions = this.actions.filter(act => act.action === action);
    if (foundActions.length > 0) {
      this.performAction(foundActions[0]);
    }
  }
}
