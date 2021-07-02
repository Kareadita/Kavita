import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { Action, ActionItem } from 'src/app/_services/action-factory.service';

@Component({
  selector: 'app-card-actionables',
  templateUrl: './card-actionables.component.html',
  styleUrls: ['./card-actionables.component.scss']
})
export class CardActionablesComponent implements OnInit {

  @Input() iconClass = 'fa-ellipsis-v';
  @Input() btnClass = '';
  @Input() actions: ActionItem<any>[] = [];
  @Input() labelBy = 'card';
  @Output() actionHandler = new EventEmitter<ActionItem<any>>();

  constructor() { }

  ngOnInit(): void {
  }

  preventClick(event: any) {
    event.stopPropagation();
    event.preventDefault();
  }

  performAction(event: any, action: ActionItem<any>) {
    this.preventClick(event);

    if (typeof action.callback === 'function') {
      this.actionHandler.emit(action);
    }
  }

}
