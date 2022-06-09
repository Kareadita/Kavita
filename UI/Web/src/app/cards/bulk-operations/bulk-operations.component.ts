import { Component, Input, OnInit } from '@angular/core';
import { Action, ActionItem } from 'src/app/_services/action-factory.service';
import { BulkSelectionService } from '../bulk-selection.service';

@Component({
  selector: 'app-bulk-operations',
  templateUrl: './bulk-operations.component.html',
  styleUrls: ['./bulk-operations.component.scss']
})
export class BulkOperationsComponent implements OnInit {

  @Input() actionCallback!: (action: Action, data: any) => void;
  topOffset: number = 0;

  get actions() {
    return this.bulkSelectionService.getActions(this.actionCallback.bind(this));
  }

  constructor(public bulkSelectionService: BulkSelectionService) { }

  ngOnInit(): void {
    const navBar = document.querySelector('.navbar');
    if (navBar) {
      this.topOffset = Math.ceil(navBar.getBoundingClientRect().height); // TODO: We can make this fixed 63px
    }
  }

  handleActionCallback(action: Action, data: any) {
    this.actionCallback(action, data);
  }

  performAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action.action, null);
    }
  }


}
