import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';

export interface CardItemTitle {
  title: string;
  linkUrl: string;
}

@Component({
  selector: 'app-card-item',
  templateUrl: './card-item.component.html',
  styleUrls: ['./card-item.component.scss']
})
export class CardItemComponent implements OnInit {

  @Input() imageUrl = '';
  @Input() title = '';
  @Output() clicked = new EventEmitter<string>();

  placeholderImage = 'assets/images/image-placeholder.jpg'; //../../..

  constructor() { }

  ngOnInit(): void {
    console.log('card item');
    console.log('imageUrl: ', this.imageUrl);
  }

  handleClick() {
    this.clicked.emit(this.title);
  }

  isNullOrEmpty(val: string) {
    return val === null || val === undefined || val === '';
  }

}
