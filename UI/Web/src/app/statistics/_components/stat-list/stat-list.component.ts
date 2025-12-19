import {ChangeDetectionStrategy, Component, computed, input, output} from '@angular/core';
import {CompactNumberPipe} from '../../../_pipes/compact-number.pipe';
import {ImageComponent} from '../../../shared/image/image.component';
import {NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {NgClass} from '@angular/common';


export interface StatListItem {
  name: string;
  value: number;
  /** Any extra data needed for click handling or image resolution */
  data?: unknown;
}

export interface StatListConfig {
  /** Show numbered rankings (1, 2, 3...) */
  showRanking?: boolean;
  /** Show colored accent bars next to rankings */
  showAccentBars?: boolean;
  /** Maximum items to display */
  maxItems?: number;
  /** Accent color for top item (CSS color) */
  accentColor?: string;
}

@Component({
    selector: 'app-stat-list',
    templateUrl: './stat-list.component.html',
    styleUrls: ['./stat-list.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgbTooltip, NgClass, ImageComponent, CompactNumberPipe]
})
export class StatListComponent {

  /** Title displayed in header */
  title = input.required<string>();

  /** Items to display */
  items = input.required<StatListItem[]>();

  /** Label shown in header and after values (e.g., "reads", "USERS") */
  valueLabel = input<string>('');

  /** Optional tooltip description */
  description = input<string>('');

  /** Featured image URL (large image on left side) */
  featuredImage = input<string | null>(null);

  /** Function to resolve item thumbnail URL */
  itemImage = input<((item: StatListItem) => string) | null>(null);

  /** Configuration options */
  config = input<StatListConfig>({});

  /** Emitted when an item is clicked. If provided, items become clickable. */
  itemClick = output<StatListItem>();

  /** Explicitly mark items as clickable (auto-detected if itemClick has subscribers in template) */
  clickable = input(false);

  protected displayItems = computed(() => {
    const cfg = this.config();
    const maxItems = cfg.maxItems ?? 5;
    return this.items().slice(0, maxItems);
  });

  protected showRanking = computed(() => this.config().showRanking ?? false);
  protected showAccentBars = computed(() => this.config().showAccentBars ?? false);
  protected accentColor = computed(() => this.config().accentColor ?? 'var(--primary-color)');

  protected hasFeaturedImage = computed(() => {
    const img = this.featuredImage();
    return img != null && img.length > 0;
  });

  protected getItemImage(item: StatListItem): string | null {
    const fn = this.itemImage();
    if (!fn) return null;
    const url = fn(item);
    return url?.length > 0 ? url : null;
  }

  protected onItemClick(item: StatListItem): void {
    if (this.clickable()) {
      this.itemClick.emit(item);
    }
  }

  protected onItemKeydown(event: KeyboardEvent, item: StatListItem): void {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.onItemClick(item);
    }
  }

}
