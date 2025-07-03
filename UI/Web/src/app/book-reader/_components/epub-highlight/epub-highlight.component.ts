import {
  AfterViewChecked,
  Component,
  computed,
  ElementRef,
  input,
  model,
  OnDestroy,
  OnInit,
  signal,
  ViewChild
} from '@angular/core';
import {Annotation} from "../../_models/annotation";

export type HighlightColor = 'blue' | 'green';

@Component({
  selector: 'app-epub-highlight',
  imports: [],
  templateUrl: './epub-highlight.component.html',
  styleUrl: './epub-highlight.component.scss'
})
export class EpubHighlightComponent implements OnInit, AfterViewChecked, OnDestroy {
  showHighlight = model<boolean>(false);
  color = input<HighlightColor>('blue');
  annotation = input<Annotation | null>(null);
  isHovered = signal<boolean>(false);

  @ViewChild('highlightSpan', { static: false }) highlightSpan!: ElementRef;

  private resizeObserver?: ResizeObserver;
  private annotationCardElement?: HTMLElement;

  showAnnotationCard = computed(() => {
    const annotation = this.annotation();
    return this.showHighlight() && true; //annotation && annotation?.noteText.length > 0;
  });

  highlightClasses = computed(() => {
    const baseClass = 'epub-highlight';

    if (!this.showHighlight()) {
      return '';
    }

    const colorClass = `epub-highlight-${this.color()}`;
    return `${colorClass}`;
  });

  cardPosition = computed(() => {
    console.log('card position called')
    if (!this.showHighlight() || !this.highlightSpan) return null;

    const rect = this.highlightSpan.nativeElement.getBoundingClientRect();
    const viewportWidth = window.innerWidth;
    const cardWidth = 200;
    const cardHeight = 80; // Approximate card height

    // Check if highlight is on left half (< 50%) or right half (>= 50%) of document
    const highlightCenterX = rect.left + (rect.width / 2);
    const isOnLeftHalf = highlightCenterX < (viewportWidth * 0.5);

    const cardLeft = isOnLeftHalf
      ? Math.max(20, rect.left - cardWidth - 20) // Left side with margin consideration
      : Math.min(viewportWidth - cardWidth - 20, rect.right + 20); // Right side

    const cardTop = rect.top + window.scrollY;

    // Calculate connection points
    const highlightCenterY = rect.top + window.scrollY + (rect.height / 2);
    const cardCenterY = cardTop + (cardHeight / 2);

    // Connection points
    const highlightPoint = {
      x: isOnLeftHalf ? rect.left : rect.right,
      y: highlightCenterY
    };

    const cardPoint = {
      x: isOnLeftHalf ? cardLeft + cardWidth : cardLeft,
      y: cardCenterY
    };

    // Calculate line properties
    const deltaX = cardPoint.x - highlightPoint.x;
    const deltaY = cardPoint.y - highlightPoint.y;
    const distance = Math.sqrt(deltaX * deltaX + deltaY * deltaY);
    const angle = Math.atan2(deltaY, deltaX) * 180 / Math.PI;

    return {
      top: cardTop,
      left: cardLeft,
      isRight: !isOnLeftHalf,
      connection: {
        startX: highlightPoint.x,
        startY: highlightPoint.y,
        endX: cardPoint.x,
        endY: cardPoint.y,
        distance: distance,
        angle: angle
      }
    };
  });

  ngOnInit() {
    // Monitor viewport changes for repositioning
    this.resizeObserver = new ResizeObserver(() => {
      // Trigger recalculation if card is visible
      if (this.showAnnotationCard()) {
        this.updateCardPosition();
      }
    });
    this.resizeObserver.observe(document.body);
  }

  ngAfterViewChecked() {
    if (this.showAnnotationCard() && this.cardPosition()) {
      this.createOrUpdateAnnotationCard();
    } else {
      this.removeAnnotationCard();
    }
  }

  ngOnDestroy() {
    this.resizeObserver?.disconnect();
  }

  onMouseEnter() {
    this.isHovered.set(true);
    if (this.annotation() && this.showAnnotationCard()) {
      //this.showAnnotationCard.update(true);
    }
  }

  onMouseLeave() {
    this.isHovered.set(false);
    //this.showAnnotationCard.set(false);
  }


  toggleHighlight() {
    this.showHighlight.set(!this.showHighlight());
  }

  updateCardPosition() {
    // TODO: Figure this out
  }

  private createOrUpdateAnnotationCard() {
    const pos = this.cardPosition();
    if (!pos) return;

    // Remove existing card if it exists
    this.removeAnnotationCard();

    // Create new card element
    this.annotationCardElement = document.createElement('div');
    this.annotationCardElement.className = `annotation-card ${this.isHovered() ? 'hovered' : ''}`;
    this.annotationCardElement.setAttribute('data-position', pos.isRight ? 'right' : 'left');
    this.annotationCardElement.style.position = 'absolute';
    this.annotationCardElement.style.top = `${pos.top}px`;
    this.annotationCardElement.style.left = `${pos.left}px`;
    this.annotationCardElement.style.zIndex = '1000';

    // Add event listeners for hover
    this.annotationCardElement.addEventListener('mouseenter', () => this.onMouseEnter());
    this.annotationCardElement.addEventListener('mouseleave', () => this.onMouseLeave());

    // Create card content
    this.annotationCardElement.innerHTML = `
      <div class="annotation-content">
        <div class="annotation-text">This is test text</div>
        <div class="annotation-meta">
          <small>10/20/2025</small>
        </div>
      </div>
    `;

    // Create connection line
    const lineElement = document.createElement('div');
    lineElement.className = `connection-line ${this.isHovered() ? 'hovered' : ''}`;
    lineElement.style.position = 'absolute';
    lineElement.style.left = `${pos.connection.startX}px`;
    lineElement.style.top = `${pos.connection.startY}px`;
    lineElement.style.width = `${pos.connection.distance}px`;
    lineElement.style.height = '2px';
    lineElement.style.backgroundColor = '#9ca3af';
    lineElement.style.transformOrigin = '0 50%';
    lineElement.style.transform = `rotate(${pos.connection.angle}deg)`;
    lineElement.style.opacity = this.isHovered() ? '1' : '0.3';
    lineElement.style.transition = 'opacity 0.2s ease';
    lineElement.style.zIndex = '999';

    // Add dot at the end
    const dotElement = document.createElement('div');
    dotElement.style.position = 'absolute';
    dotElement.style.right = '-3px';
    dotElement.style.top = '50%';
    dotElement.style.width = '6px';
    dotElement.style.height = '6px';
    dotElement.style.backgroundColor = '#9ca3af';
    dotElement.style.borderRadius = '50%';
    dotElement.style.transform = 'translateY(-50%)';

    lineElement.appendChild(dotElement);

    // Append to body
    document.body.appendChild(this.annotationCardElement);
    document.body.appendChild(lineElement);

    // Store reference to line for updates
    (this.annotationCardElement as any).lineElement = lineElement;
  }

  private removeAnnotationCard() {
    if (this.annotationCardElement) {
      // Remove associated line element
      const lineElement = (this.annotationCardElement as any).lineElement;
      if (lineElement) {
        lineElement.remove();
      }

      this.annotationCardElement.remove();
      this.annotationCardElement = undefined;
    }
  }

  private updateLineOpacity() {
    if (this.annotationCardElement) {
      const lineElement = (this.annotationCardElement as any).lineElement;
      if (lineElement) {
        lineElement.style.opacity = this.isHovered() ? '1' : '0.3';
      }
    }
  }
}
