import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  Input,
  OnDestroy,
  OnInit
} from '@angular/core';
import { Subject, takeUntil } from 'rxjs';
import { Action, ActionFactoryService, ActionItem } from 'src/app/_services/action-factory.service';
import { BulkSelectionService } from '../bulk-selection.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

@Component({
  selector: 'app-bulk-operations',
  templateUrl: './bulk-operations.component.html',
  styleUrls: ['./bulk-operations.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BulkOperationsComponent implements OnInit {

  @Input({required: true}) actionCallback!: (action: ActionItem<any>, data: any) => void;

  topOffset: number = 56;
  hasMarkAsRead: boolean = false;
  hasMarkAsUnread: boolean = false;
  actions: Array<ActionItem<any>> = [];
  private readonly destroyRef = inject(DestroyRef);

  get Action() {
    return Action;
  }

  constructor(public bulkSelectionService: BulkSelectionService, private readonly cdRef: ChangeDetectorRef,
    private actionFactoryService: ActionFactoryService) { }

  ngOnInit(): void {
    this.bulkSelectionService.actions$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(actions => {
      // We need to do a recursive callback apply
      this.actions = this.actionFactoryService.applyCallbackToList(actions, this.actionCallback.bind(this));
      this.hasMarkAsRead = this.actionFactoryService.hasAction(this.actions, Action.MarkAsRead);
      this.hasMarkAsUnread = this.actionFactoryService.hasAction(this.actions, Action.MarkAsUnread);
      this.cdRef.markForCheck();
    });
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
