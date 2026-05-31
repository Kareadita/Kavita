import {ChangeDetectionStrategy, Component, input, model} from '@angular/core';

let nextId = 0;

/**
 * Reusable, slot-based accordion tile.
 *
 * Header slots (project via attribute selectors, render only what you need):
 *  - `[accLead]`     optional leading icon/logo tile
 *  - `[accTitle]`    required title
 *  - `[accSubtitle]` optional subtitle
 *  - `[accBadges]`   optional status pills beside the title
 *  - `[accCta]`      optional header action buttons (clicks are isolated from the toggle)
 *  - `[accMeta]`     optional right-aligned meta text (e.g. a timestamp)
 *  - default slot    projected body content
 *
 * The chevron and open/close mechanics belong to the component. Exclusivity (single-open)
 * is intentionally not a feature here; a parent drives `open` per instance if it wants that.
 */
@Component({
  selector: 'app-accordion',
  templateUrl: './accordion.component.html',
  styleUrls: ['./accordion.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[class.is-open]': 'open()',
    '[class.toggle-chevron]': "toggleTrigger() === 'chevron'",
  }
})
export class AccordionComponent {
  /**
   * Whether the panel is expanded. Two-way bindable.
   */
  open = model<boolean>(false);
  /**
   * What toggles the panel: the whole header row ('header', default) or only the chevron
   * button ('chevron'). Use 'chevron' when the header contains its own interactive layout.
   */
  toggleTrigger = input<'header' | 'chevron'>('header');
  /**
   * Accessible label for the chevron toggle button (only used when toggleTrigger is 'chevron').
   */
  toggleLabel = input<string>('');
  /**
   * When set, the title is exposed to assistive tech as a heading at this level (role="heading"
   * + aria-level), so a list of accordions remains navigable by heading. Leave null for a plain title.
   */
  headingLevel = input<number | null>(null);

  protected readonly bodyId = `app-accordion-body-${nextId++}`;

  toggle() {
    this.open.update(v => !v);
  }

  onHeaderClick() {
    if (this.toggleTrigger() === 'header') this.toggle();
  }

  onHeaderKey(event: Event) {
    if (this.toggleTrigger() !== 'header') return;
    // Only toggle when the header row itself is the key target; ignore keys bubbling
    // up from projected CTA buttons/inputs so they don't accidentally toggle the panel.
    if (event.target !== event.currentTarget) return;
    event.preventDefault();
    this.toggle();
  }

  onChevronClick(event: MouseEvent) {
    event.stopPropagation();
    this.toggle();
  }
}
