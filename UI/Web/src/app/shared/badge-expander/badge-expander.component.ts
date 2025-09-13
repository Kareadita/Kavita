import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ContentChild, EventEmitter,
  inject,
  Input, OnChanges,
  OnInit, Output, SimpleChanges,
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
export class BadgeExpanderComponent implements OnInit, OnChanges {

  private readonly cdRef = inject(ChangeDetectorRef);

  @Input() items: Array<any> = [];
  @Input() itemsTillExpander: number = 4;
  @Input() allowToggle: boolean = true;
  @Input() includeComma: boolean = true;
  /**
   * If should be expanded by default. Defaults to false.
   */
  @Input() defaultExpanded: boolean = false;
  /**
   * Invoked when the "and more" is clicked
   */
  @Output() toggle = new EventEmitter<void>();
  @ContentChild('badgeExpanderItem') itemTemplate!: TemplateRef<any>;


  visibleItems: Array<any> = [];
  isCollapsed: boolean = false;

  get itemsLeft() {
    if (this.defaultExpanded) return 0;

    return Math.max(this.items.length - this.itemsTillExpander, 0);
  }

  ngOnInit(): void {

    if (this.defaultExpanded) {
      this.isCollapsed = false;
      this.visibleItems = this.items;
      this.cdRef.markForCheck();
      return;
    }

    this.visibleItems = this.items.slice(0, this.itemsTillExpander);
    this.cdRef.markForCheck();
  }

  ngOnChanges(changes: SimpleChanges) {
    this.visibleItems = this.items.slice(0, this.itemsTillExpander);
    this.cdRef.markForCheck();
  }

  toggleVisible() {
    this.toggle.emit();
    if (!this.allowToggle) return;

    this.isCollapsed = !this.isCollapsed;
    this.visibleItems = this.items;
    this.cdRef.markForCheck();
  }

}
