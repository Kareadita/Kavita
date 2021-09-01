import { CdkDragDrop, moveItemInArray } from '@angular/cdk/drag-drop';
import { Component, ContentChild, Input, OnInit, TemplateRef } from '@angular/core';

@Component({
  selector: 'app-dragable-ordered-list',
  templateUrl: './dragable-ordered-list.component.html',
  styleUrls: ['./dragable-ordered-list.component.scss']
})
export class DragableOrderedListComponent implements OnInit {

  @Input() items: Array<any> = [];
  @ContentChild('draggableItem') itemTemplate!: TemplateRef<any>;

  constructor() { }

  ngOnInit(): void {
  }

  drop(event: CdkDragDrop<string[]>) {
    moveItemInArray(this.items, event.previousIndex, event.currentIndex);
  }

  updateIndex(previousIndex: number) {
    // get the new value of the input 
    var inputElem = <HTMLInputElement>document.querySelector('#reorder-' + previousIndex);
    const newIndex = parseInt(inputElem.value, 10);
    moveItemInArray(this.items, previousIndex, newIndex);
  }

}
