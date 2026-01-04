import {ChangeDetectionStrategy, Component, computed, input, output, signal} from '@angular/core';
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

  /** Featured image URL (large image on left side). If itemImage is provided, this acts as default. */
  featuredImage = input<string | null>(null);

  /** Function to resolve item thumbnail URL */
  itemImage = input<((item: StatListItem) => string | null) | null>(null);

  /** Function to resolve item navigation URL. If provided, items render as anchors. */
  itemUrl = input<((item: StatListItem) => string | null) | null>(null);

  /** Configuration options */
  config = input<StatListConfig>({});

  /** Emitted when an item is clicked (only fires if no itemUrl or for non-navigation clicks). */
  itemClick = output<StatListItem>();

  /** Explicitly mark items as clickable (auto-detected if itemClick has subscribers in template) */
  clickable = input(false);

  /** Currently hovered item for dynamic featured image */
  private hoveredItem = signal<StatListItem | null>(null);

  protected displayItems = computed(() => {
    const cfg = this.config();
    const maxItems = cfg.maxItems ?? 5;
    return this.items().slice(0, maxItems);
  });

  protected showRanking = computed(() => this.config().showRanking ?? false);
  protected showAccentBars = computed(() => this.config().showAccentBars ?? false);
  protected accentColor = computed(() => this.config().accentColor ?? 'var(--primary-color)');

  /** Computed featured image - shows hovered item's image or falls back to default */
  protected displayedFeaturedImage = computed(() => {
    const hovered = this.hoveredItem();
    const itemImageFn = this.itemImage();

    // If hovering and we have an item image function, use hovered item's image
    if (hovered && itemImageFn) {
      const hoveredImg = itemImageFn(hovered);
      if (hoveredImg?.length) {
        return hoveredImg;
      }
    }

    return this.featuredImage();
  });

  protected hasFeaturedImage = computed(() => {
    // Show featured area if we have a default image OR if itemImage is provided (for hover behavior)
    const img = this.featuredImage();
    const hasDefault = img != null && img.length > 0;
    const hasItemImageFn = this.itemImage() != null;
    return hasDefault || hasItemImageFn;
  });

  protected getItemImage(item: StatListItem): string | null {
    const fn = this.itemImage();
    if (!fn) return null;
    const url = fn(item);
    return url?.length ? url : null;
  }

  protected getItemUrl(item: StatListItem): string | null {
    const fn = this.itemUrl();
    if (!fn) return null;
    const url = fn(item);
    return url?.length ? url : null;
  }

  protected hasItemUrl = computed(() => this.itemUrl() != null);

  protected onItemMouseEnter(item: StatListItem): void {
    if (this.itemImage()) {
      this.hoveredItem.set(item);
    }
  }

  protected onItemMouseLeave(): void {
    this.hoveredItem.set(null);
  }

  protected onItemClick(event: Event, item: StatListItem): void {
    // If we have a URL function and it returns a URL, let the anchor handle navigation
    if (this.getItemUrl(item)) {
      return; // Anchor handles it
    }

    // Otherwise emit click event if clickable
    if (this.clickable()) {
      event.preventDefault();
      this.itemClick.emit(item);
    }
  }

  protected onItemKeydown(event: KeyboardEvent, item: StatListItem): void {
    if (event.key === 'Enter' || event.key === ' ') {
      // If it's an anchor with URL, let default behavior handle Enter
      if (this.getItemUrl(item) && event.key === 'Enter') {
        return;
      }

      // For space on anchors or any key on non-anchor clickables
      if (this.clickable() || this.getItemUrl(item)) {
        event.preventDefault();
        if (this.getItemUrl(item)) {
          window.open(this.getItemUrl(item)!, '_blank', 'noopener,noreferrer');
        } else {
          this.itemClick.emit(item);
        }
      }
    }
  }
}
