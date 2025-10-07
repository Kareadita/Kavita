import {
  ChangeDetectionStrategy,
  Component, computed,
  ContentChild, EventEmitter,
  input,
  OnInit, Output, signal,
  TemplateRef
} from '@angular/core';
import {NgTemplateOutlet} from "@angular/common";
import {TranslocoDirective} from "@jsverse/transloco";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";

@Component({
    selector: 'app-badge-expander',
    imports: [TranslocoDirective, NgTemplateOutlet, DefaultValuePipe],
    templateUrl: './badge-expander.component.html',
    styleUrls: ['./badge-expander.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class BadgeExpanderComponent implements OnInit {

  items = input.required<any[]>();
  itemsTillExpander = input(4);
  allowToggle = input(true);
  includeComma = input(true);
  /**
   * If the list should be expanded by default. Defaults to false.
   */
  defaultExpanded = input(false);

  /**
   * Invoked when the "and more" is clicked
   */
  @Output() toggle = new EventEmitter<void>();
  @ContentChild('badgeExpanderItem') itemTemplate!: TemplateRef<any>;

  isCollapsed = signal<boolean | undefined>(undefined);
  visibleItems = computed(() => {
    const allItems = this.items();
    const isCollapsed = this.isCollapsed();
    const cutOff = this.itemsTillExpander();

    if (!isCollapsed) return allItems;

    return allItems.slice(0, cutOff);
  });
  itemsLeft = computed(() => {
    const allItems = this.items();
    const visibleItems = this.visibleItems();

    return allItems.length - visibleItems.length;
  });

  ngOnInit(): void {
    this.isCollapsed.set(!this.defaultExpanded());
  }

  toggleVisible() {
    this.toggle.emit();
    if (!this.allowToggle()) return;

    this.isCollapsed.update(x => !x);
  }

}
