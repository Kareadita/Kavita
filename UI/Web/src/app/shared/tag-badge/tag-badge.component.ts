import { Component, Input, OnInit } from '@angular/core';

/**
 * What type of cursor to apply to the tag badge
 */
export enum TagBadgeCursor {
  /**
   * Allows the user to select text
   * cursor: default 
   */
  Selectable,
  /**
   * Informs the user they can click and interact with badge
   * cursor: pointer
   */
  Clickable,
  /**
   * Informs the user they cannot click or interact with badge
   * cursor: not-allowed
   */
  NotAllowed,
}

@Component({
  selector: 'app-tag-badge',
  templateUrl: './tag-badge.component.html',
  styleUrls: ['./tag-badge.component.scss']
})
export class TagBadgeComponent implements OnInit {

  @Input() selectionMode: TagBadgeCursor = TagBadgeCursor.Selectable;

  cursor: string = 'default';

  constructor() { }

  ngOnInit(): void {
    switch (this.selectionMode) {
      case TagBadgeCursor.Selectable:
        this.cursor = 'selectable-cursor';
        break;
      case TagBadgeCursor.NotAllowed:
        this.cursor = 'not-allowed-cursor';
        break;
      case TagBadgeCursor.Clickable:
        this.cursor = 'clickable-cursor';
        break;
    }
  }

}
