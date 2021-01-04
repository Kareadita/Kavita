import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';


export interface CardItemAction {
  title: string;
  callback: (data: any) => void;
}

@Component({
  selector: 'app-card-item',
  templateUrl: './card-item.component.html',
  styleUrls: ['./card-item.component.scss']
})
export class CardItemComponent implements OnInit {

  @Input() imageUrl = '';
  @Input() title = '';
  @Input() actions: CardItemAction[] = [];
  @Input() entity: any; // This is the entity we are representing. It will be returned if an action is executed.
  @Output() clicked = new EventEmitter<string>();

  safeImage: any;
  placeholderImage = 'assets/images/image-placeholder.jpg';

  constructor(private sanitizer: DomSanitizer) { }

  ngOnInit(): void {
    this.createSafeImage(this.imageUrl);
  }

  handleClick() {
    this.clicked.emit(this.title);
  }

  isNullOrEmpty(val: string) {
    return val === null || val === undefined || val === '';
  }

  performAction(event: any, action: CardItemAction) {
    event.stopPropagation();
    if (typeof action.callback === 'function') {
      action.callback(this.entity);
    }
  }

  createSafeImage(coverImage: string) {
    let imageUrl = 'data:image/jpeg;base64,' + coverImage;  
    this.safeImage = this.sanitizer.bypassSecurityTrustUrl(imageUrl);
  }

}
