import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ContentChild,
  inject,
  Input,
  OnInit,
  TemplateRef
} from '@angular/core';
import {CommonModule, NgTemplateOutlet} from "@angular/common";
import {TranslocoDirective} from "@jsverse/transloco";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";

@Component({
  selector: 'app-badge-expander',
  standalone: true,
  imports: [TranslocoDirective, NgTemplateOutlet, DefaultValuePipe],
  templateUrl: './badge-expander.component.html',
  styleUrls: ['./badge-expander.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BadgeExpanderComponent implements OnInit {

  private readonly cdRef = inject(ChangeDetectorRef);

  @Input() items: Array<any> = [];
  @Input() itemsTillExpander: number = 4;
  @Input() allowToggle: boolean = true;
  @ContentChild('badgeExpanderItem') itemTemplate!: TemplateRef<any>;


  visibleItems: Array<any> = [];
  isCollapsed: boolean = false;

  get itemsLeft() {
    return Math.max(this.items.length - this.itemsTillExpander, 0);
  }

  ngOnInit(): void {
    this.visibleItems = this.items.slice(0, this.itemsTillExpander);
    this.cdRef.markForCheck();
  }

  toggleVisible() {
    if (!this.allowToggle) return;

    this.isCollapsed = !this.isCollapsed;
    this.visibleItems = this.items;
    this.cdRef.markForCheck();
  }

}
