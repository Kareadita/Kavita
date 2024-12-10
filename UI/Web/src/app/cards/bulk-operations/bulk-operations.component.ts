import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef, HostListener,
  inject,
  Input,
  OnInit
} from '@angular/core';
import { Action, ActionFactoryService, ActionItem } from 'src/app/_services/action-factory.service';
import { BulkSelectionService } from '../bulk-selection.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {AsyncPipe, DecimalPipe, NgStyle} from "@angular/common";
import {TranslocoModule} from "@jsverse/transloco";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {CardActionablesComponent} from "../../_single-module/card-actionables/card-actionables.component";
import {KEY_CODES} from "../../shared/_services/utility.service";

@Component({
  selector: 'app-bulk-operations',
  standalone: true,
  imports: [
    AsyncPipe,
    CardActionablesComponent,
    TranslocoModule,
    NgbTooltip,
    NgStyle,
    DecimalPipe
  ],
  templateUrl: './bulk-operations.component.html',
  styleUrls: ['./bulk-operations.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BulkOperationsComponent implements OnInit {

  @Input({required: true}) actionCallback!: (action: ActionItem<any>, data: any) => void;
  /**
   * Modal mode means don't fix to the top
   */
  @Input() modalMode = false;
  /**
   * On Series Detail this should be 12
   */
  @Input() marginLeft: number = 0;
  /**
   * On Series Detail this should be 12
   */
  @Input() marginRight: number = 8;
  hasMarkAsRead: boolean = false;
  hasMarkAsUnread: boolean = false;
  actions: Array<ActionItem<any>> = [];

  private readonly destroyRef = inject(DestroyRef);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly actionFactoryService = inject(ActionFactoryService);
  public readonly bulkSelectionService = inject(BulkSelectionService);
  protected readonly Action = Action;

  @HostListener('document:keydown.shift', ['$event'])
  handleKeypress(event: KeyboardEvent) {
    if (event.key === KEY_CODES.SHIFT) {
      this.bulkSelectionService.isShiftDown = true;
    }
    // TODO: See if we can figure out a select all (Ctrl+A) by having each method handle the event or pass all the data into this component.
  }

  @HostListener('document:keyup.shift', ['$event'])
  handleKeyUp(event: KeyboardEvent) {
    if (event.key === KEY_CODES.SHIFT) {
      this.bulkSelectionService.isShiftDown = false;
    }
  }

  ngOnInit(): void {
    this.bulkSelectionService.actions$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(actions => {
      // We need to do a recursive callback apply
      this.actions = this.actionFactoryService.applyCallbackToList(actions, this.actionCallback.bind(this));
      this.hasMarkAsRead = this.actionFactoryService.hasAction(this.actions, Action.MarkAsRead);
      this.hasMarkAsUnread = this.actionFactoryService.hasAction(this.actions, Action.MarkAsUnread);
      this.cdRef.markForCheck();
    });
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
