import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ContentChild,
  ContentChildren, ElementRef, EventEmitter, HostListener,
  inject, Input, OnInit, Output, QueryList,
  TemplateRef, ViewChild
} from '@angular/core';
import {
  NgbNav,
  NgbNavChangeEvent,
  NgbNavContent,
  NgbNavItem,
  NgbNavLink,
  NgbNavOutlet
} from "@ng-bootstrap/ng-bootstrap";
import {CarouselTabComponent} from "../carousel-tab/carousel-tab.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {NgTemplateOutlet} from "@angular/common";

/**
 * Any Tabs that use this Carousel should use these
 */
export enum TabId {
  Related = 'related-tab',
  Reviews = 'review-tab', // Only applicable for books
  Details = 'details-tab',
  Chapters = 'chapters-tab',
}

@Component({
  selector: 'app-carousel-tabs',
  standalone: true,
  imports: [
    NgbNav,
    TranslocoDirective,
    NgbNavItem,
    NgbNavLink,
    NgTemplateOutlet,
    NgbNavOutlet,
    NgbNavContent
  ],
  templateUrl: './carousel-tabs.component.html',
  styleUrl: './carousel-tabs.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CarouselTabsComponent implements OnInit, AfterViewInit {
  private readonly cdRef = inject(ChangeDetectorRef);

  @ContentChildren(CarouselTabComponent) tabComponents!: QueryList<CarouselTabComponent>;

  @Input({required: true}) activeTabId!: TabId;
  @Output() activeTabIdChange = new EventEmitter<TabId>();
  @Output() navChange = new EventEmitter<NgbNavChangeEvent>();

  @ViewChild('scrollContainer') scrollContainer: ElementRef | undefined;

  tabs: { id: TabId; contentTemplate: any }[] = [];
  showLeftArrow = false;
  showRightArrow = false;


  ngOnInit() {
    this.checkOverflow();
  }

  ngAfterViewInit() {
    this.initializeTabs();
    this.scrollToActiveTab();
    this.checkOverflow();
  }

  initializeTabs() {
    this.tabs = this.tabComponents.map(tabComponent => ({
      id: tabComponent.id,
      contentTemplate: tabComponent.implicitContent
    }));
    this.cdRef.markForCheck();
  }

  @HostListener('window:resize')
  onResize() {
    this.checkOverflow();
  }

  onNavChange(event: NgbNavChangeEvent) {
    this.activeTabIdChange.emit(event.nextId);
    this.navChange.emit(event);
    this.scrollToActiveTab();
  }

  onScroll() {
    this.checkOverflow();
  }

  scrollToActiveTab() {
    setTimeout(() => {
      const activeTab = this.scrollContainer?.nativeElement.querySelector('.active');
      if (activeTab) {
        activeTab.scrollIntoView({ behavior: 'smooth', inline: 'center', block: 'nearest' });
      }
      this.checkOverflow();
    });
  }

  checkOverflow() {
    const element = this.scrollContainer?.nativeElement;
    if (!element) return;
    this.showLeftArrow = element.scrollLeft > 0;
    this.showRightArrow = element.scrollLeft < element.scrollWidth - element.clientWidth;
    this.cdRef.markForCheck();
  }

  scroll(direction: 'left' | 'right') {
    const element = this.scrollContainer?.nativeElement;
    if (!element) return;
    const scrollAmount = element.clientWidth / 2;
    if (direction === 'left') {
      element.scrollBy({ left: -scrollAmount, behavior: 'smooth' });
    } else {
      element.scrollBy({ left: scrollAmount, behavior: 'smooth' });
    }
  }

}
