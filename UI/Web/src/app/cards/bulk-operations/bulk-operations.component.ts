import {ChangeDetectionStrategy, Component, computed, HostListener, inject, input} from '@angular/core';
import {BulkSelectionService} from '../bulk-selection.service';
import {DecimalPipe} from "@angular/common";
import {TranslocoModule} from "@jsverse/transloco";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {CardActionablesComponent} from "../../_single-module/card-actionables/card-actionables.component";
import {KEY_CODES} from "../../shared/_services/utility.service";
import {ActionItem} from "../../_models/actionables/action-item";
import {Action} from "../../_models/actionables/action";
import {ActionFactoryService} from "../../_services/action-factory.service";
import {ActionResult} from "../../_models/actionables/action-result";

@Component({
  selector: 'app-bulk-operations',
  imports: [
    CardActionablesComponent,
    TranslocoModule,
    NgbTooltip,
    DecimalPipe
  ],
  templateUrl: './bulk-operations.component.html',
  styleUrls: ['./bulk-operations.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BulkOperationsComponent<T> {

  private readonly actionFactoryService = inject(ActionFactoryService);
  protected readonly bulkSelectionService = inject(BulkSelectionService);

  /**
   * On Series Detail this should be 0
   */
  marginLeft = input<number>(0);
  /**
   * On Series Detail this should be 0
   */
  marginRight = input<number>(8);

  actions = computed(() => this.bulkSelectionService.actionsSignal() ?? []);
  hasMarkAsRead = computed(() => this.actionFactoryService.hasAction(this.actions(), Action.MarkAsRead));
  hasMarkAsUnread = computed(() => this.actionFactoryService.hasAction(this.actions(), Action.MarkAsUnread));


  @HostListener('document:keydown.shift', ['$event'])
  handleKeypress(event: Event) {
    const evt = event as KeyboardEvent;
    if (evt.key === KEY_CODES.SHIFT) {
      this.bulkSelectionService.isShiftDown = true;
    }
    // TODO: See if we can figure out a select all (Ctrl+A) by having each method handle the event or pass all the data into this component.
  }

  @HostListener('document:keyup.shift', ['$event'])
  handleKeyUp(event: Event) {
    const evt = event as KeyboardEvent;
    if (evt.key === KEY_CODES.SHIFT) {
      this.bulkSelectionService.isShiftDown = false;
    }
  }

  performAction(event: ActionItem<any> | ActionResult<any>) {
    // Skip ActionResults — they've already been handled
    if ('effect' in event) return;

    event.callback(event, null).subscribe();
  }

  executeAction(action: Action) {
    const foundActions = this.actions().filter(act => act.action === action);
    if (foundActions.length > 0) {
      this.performAction(foundActions[0]);
    }
  }

  protected readonly Action = Action;
}
