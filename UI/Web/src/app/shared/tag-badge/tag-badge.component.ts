import {ChangeDetectionStrategy, Component, input} from '@angular/core';

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

export type TagBadgeColor = 'default' | 'primary' | 'secondary' | 'error';

@Component({
    selector: 'app-tag-badge',
    templateUrl: './tag-badge.component.html',
    styleUrls: ['./tag-badge.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class TagBadgeComponent {

  selectionMode = input<TagBadgeCursor>(TagBadgeCursor.Selectable);
  fillStyle = input<'filled' | 'outline'>('outline');
  color = input<TagBadgeColor>('default');
  size = input<'default' | 'sm'>('default');
  shape = input<'default' | 'pill'>('default');

  protected readonly TagBadgeCursor = TagBadgeCursor;
}
