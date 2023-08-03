import { ChangeDetectionStrategy, ChangeDetectorRef, Component, ContentChild, Input, OnInit, TemplateRef } from '@angular/core';
import {CommonModule} from "@angular/common";
import {TranslocoModule} from "@ngneat/transloco";

@Component({
  selector: 'app-badge-expander',
  standalone: true,
  imports: [CommonModule, TranslocoModule],
  templateUrl: './badge-expander.component.html',
  styleUrls: ['./badge-expander.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BadgeExpanderComponent implements OnInit {

  @Input() items: Array<any> = [];
  @Input() itemsTillExpander: number = 4;
  @ContentChild('badgeExpanderItem') itemTemplate!: TemplateRef<any>;


  visibleItems: Array<any> = [];
  isCollapsed: boolean = false;

  get itemsLeft() {
    return Math.max(this.items.length - this.itemsTillExpander, 0);
  }

  constructor(private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit(): void {
    this.visibleItems = this.items.slice(0, this.itemsTillExpander);
    this.cdRef.markForCheck();
  }

  toggleVisible() {
    this.isCollapsed = !this.isCollapsed;
    this.visibleItems = this.items;
    this.cdRef.markForCheck();
  }

}
