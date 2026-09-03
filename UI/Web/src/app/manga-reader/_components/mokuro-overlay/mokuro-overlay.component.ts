import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  Input,
  OnChanges,
  OnDestroy,
} from '@angular/core';
import {NgStyle} from '@angular/common';
import {MokuroBlock, MokuroPage} from '../../_models/mokuro';

interface MokuroLineLayout {
  text: string;
  style: Record<string, string>;
}

interface MokuroBlockLayout {
  ariaLabel: string;
  vertical: boolean;
  style: Record<string, string>;
  lines: MokuroLineLayout[];
}

@Component({
  selector: 'app-mokuro-overlay',
  templateUrl: './mokuro-overlay.component.html',
  styleUrls: ['./mokuro-overlay.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgStyle],
})
export class MokuroOverlayComponent implements AfterViewInit, OnChanges, OnDestroy {
  private readonly elementRef = inject<ElementRef<HTMLElement>>(ElementRef);

  @Input() page: MokuroPage | null | undefined = null;
  @Input() image: HTMLImageElement | undefined;

  layouts: MokuroBlockLayout[] = [];
  private resizeObserver?: ResizeObserver;
  private animationFrame?: number;

  ngAfterViewInit(): void {
    this.observeImage();
  }

  ngOnChanges(): void {
    this.layouts = this.buildLayouts();
    this.observeImage();
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
    if (this.animationFrame !== undefined) cancelAnimationFrame(this.animationFrame);
  }

  private area(block: MokuroBlock): number {
    const [left, top, right, bottom] = block.box;
    return Math.max(0, right - left) * Math.max(0, bottom - top);
  }

  private lineBox(block: MokuroBlock, lineIndex: number): [number, number, number, number] {
    const points = block.lines_coords?.[lineIndex];
    if (points && points.length > 0) {
      const xs = points.map(point => point[0]).filter(Number.isFinite);
      const ys = points.map(point => point[1]).filter(Number.isFinite);
      if (xs.length > 0 && ys.length > 0) {
        return [Math.min(...xs), Math.min(...ys), Math.max(...xs), Math.max(...ys)];
      }
    }

    const [left, top, right, bottom] = block.box;
    const lineCount = Math.max(1, block.lines.length);
    if (block.vertical) {
      const width = (right - left) / lineCount;
      const lineRight = right - lineIndex * width;
      return [lineRight - width, top, lineRight, bottom];
    }

    const height = (bottom - top) / lineCount;
    const lineTop = top + lineIndex * height;
    return [left, lineTop, right, lineTop + height];
  }

  private buildLayouts(): MokuroBlockLayout[] {
    if (!this.page || this.page.img_width <= 0 || this.page.img_height <= 0) return [];

    return [...this.page.blocks]
      .sort((a, b) => this.area(b) - this.area(a))
      .map(block => this.buildBlockLayout(block));
  }

  private buildBlockLayout(block: MokuroBlock): MokuroBlockLayout {
    const [blockLeft, blockTop, blockRight, blockBottom] = block.box;
    const blockWidth = Math.max(1, blockRight - blockLeft);
    const blockHeight = Math.max(1, blockBottom - blockTop);
    let flowOffset = 0;

    const lines = block.lines.map((text, lineIndex) => {
      const [left, top, right, bottom] = this.lineBox(block, lineIndex);
      const lineWidth = Math.max(1, right - left);
      const lineHeight = Math.max(1, bottom - top);
      const flowLeft = block.vertical ? blockWidth - flowOffset - lineWidth : 0;
      const flowTop = block.vertical ? 0 : flowOffset;
      flowOffset += block.vertical ? lineWidth : lineHeight;

      return {
        text,
        style: {
          width: `${lineWidth}px`,
          height: `${lineHeight}px`,
          fontSize: `${this.fittedFontSize(block, text, lineWidth, lineHeight)}px`,
          writingMode: block.vertical ? 'vertical-rl' : 'horizontal-tb',
          transform: `translate(${left - blockLeft - flowLeft}px, ${top - blockTop - flowTop}px)`,
        },
      };
    });

    return {
      ariaLabel: block.lines.join(' '),
      vertical: block.vertical,
      style: {
        left: `${blockLeft}px`,
        top: `${blockTop}px`,
        width: `${blockWidth}px`,
        height: `${blockHeight}px`,
      },
      lines,
    };
  }

  private fittedFontSize(block: MokuroBlock, text: string, width: number, height: number): number {
    const characterCount = Math.max(1, Array.from(text.replace(/\s/g, '')).length);
    const primarySize = block.vertical ? height : width;
    const crossSize = block.vertical ? width : height;

    return Math.max(1, Math.min(block.font_size, primarySize / characterCount, crossSize) * 0.92);
  }

  private observeImage(): void {
    this.resizeObserver?.disconnect();
    if (!this.image) return;

    this.resizeObserver = new ResizeObserver(() => this.scheduleSync());
    this.resizeObserver.observe(this.image);
    const parent = this.elementRef.nativeElement.parentElement;
    if (parent) this.resizeObserver.observe(parent);
    this.scheduleSync();
  }

  private scheduleSync(): void {
    if (this.animationFrame !== undefined) cancelAnimationFrame(this.animationFrame);
    this.animationFrame = requestAnimationFrame(() => this.syncToImage());
  }

  private syncToImage(): void {
    this.animationFrame = undefined;
    const host = this.elementRef.nativeElement;
    const parent = host.parentElement;
    if (!this.image || !parent || !this.page) {
      host.style.display = 'none';
      return;
    }

    const imageRect = this.image.getBoundingClientRect();
    const parentRect = parent.getBoundingClientRect();
    if (imageRect.width <= 0 || imageRect.height <= 0) {
      host.style.display = 'none';
      return;
    }

    host.style.display = 'block';
    host.style.left = `${imageRect.left - parentRect.left}px`;
    host.style.top = `${imageRect.top - parentRect.top}px`;
    host.style.width = `${this.page.img_width}px`;
    host.style.height = `${this.page.img_height}px`;
    host.style.transform = `scale(${imageRect.width / this.page.img_width})`;
  }
}
