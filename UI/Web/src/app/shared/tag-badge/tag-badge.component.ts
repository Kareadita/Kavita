import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import {CommonModule} from "@angular/common";

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
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tag-badge.component.html',
  styleUrls: ['./tag-badge.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TagBadgeComponent {

  @Input() selectionMode: TagBadgeCursor = TagBadgeCursor.Selectable;
  @Input() fillStyle: 'filled' | 'outline' = 'outline';

  get TagBadgeCursor() {
    return TagBadgeCursor;
  }
}
