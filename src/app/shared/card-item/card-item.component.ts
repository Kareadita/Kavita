import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { ActionItem } from 'src/app/_services/action-factory.service';
import { ImageService } from 'src/app/_services/image.service';

@Component({
  selector: 'app-card-item',
  templateUrl: './card-item.component.html',
  styleUrls: ['./card-item.component.scss']
})
export class CardItemComponent implements OnInit {

  @Input() imageUrl = '';
  @Input() title = '';
  @Input() actions: ActionItem<any>[] = [];
  @Input() read = 0; // Pages read
  @Input() total = 0; // Total Pages
  @Input() entity: any; // This is the entity we are representing. It will be returned if an action is executed.
  @Output() clicked = new EventEmitter<string>();

  constructor(public imageSerivce: ImageService) { }

  ngOnInit(): void {
  }

  handleClick() {
    this.clicked.emit(this.title);
  }

  isNullOrEmpty(val: string) {
    return val === null || val === undefined || val === '';
  }

  preventClick(event: any) {
    event.stopPropagation();
    event.preventDefault();
  }

  performAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action.action, this.entity);
    }
  }
}
