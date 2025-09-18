import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  input, OnInit,
  Renderer2
} from '@angular/core';
import {DOCUMENT} from "@angular/common";
import {filter, fromEvent, merge, tap} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

interface Dimensions {
  width: number;
  height: number;
}

export enum ResizeMode {
  Width = "width",
  Height = "height",
}

@Component({
  selector: 'app-off-canvas-resize',
  imports: [],
  templateUrl: './off-canvas-resize.component.html',
  styleUrl: './off-canvas-resize.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class OffCanvasResizeComponent implements OnInit {

  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);
  private readonly el = inject(ElementRef);
  private readonly renderer = inject(Renderer2);

  /**
   * Minimum height of the canvas element in viewport
   */
  minHeight = input<number>(20);
  /**
   * Maximum height of the canvas element in viewport
   */
  maxHeight = input<number>(90);
  /**
   * Minimum width of the canvas element in viewport
   */
  minWidth = input<number>(20);
  /**
   * Maximum width of the canvas element in viewport
   */
  maxWidth = input<number>(90);

  /**
   * Set userSelect to none on drag start
   */
  interruptSelection = input(true);
  /**
   * If true, cancels mouse events
   */
  cancelEvents = input(true);
  /**
   * If we should resize height or width
   */
  resizeMode = input.required<ResizeMode>();
  /**
   * The positions you've opened the canvas at
   */
  canvasPosition = input.required<"start" | "end" | "top" | "bottom">();

  signMul = computed(() => this.canvasPosition() === 'top' || this.canvasPosition() === 'start' ? -1 : 1);

  dragIndicatorClass = computed(() => this.resizeMode() === ResizeMode.Height  ? 'drag-indicator-horizontal' : 'drag-indicator-vertical');


  canvasElement!: HTMLElement;

  isDragging = false;
  startDimensionsElement: Dimensions = {height: 0, width: 0};
  startDimensionsClient: Dimensions = {height: 0, width: 0};

  startDrag(event: MouseEvent | TouchEvent): void {
    if (this.cancelEvents()) {
      event.preventDefault();
    }


    this.isDragging = true;
    this.startDimensionsClient = this.getClientDimensions(event);
    this.startDimensionsElement = this.getElementDimensions();

    if (this.interruptSelection()) {
      document.body.style.userSelect = 'none';
    }
  }

  ngOnInit(): void {
    this.canvasElement = this.document.querySelector(".offcanvas-" + this.canvasPosition()) as HTMLElement;

    const mouseMove$ = fromEvent<MouseEvent>(this.document, 'mousemove');
    const touchMove$ = fromEvent<TouchEvent>(this.document, 'touchmove');

    merge(mouseMove$, touchMove$).pipe(
      takeUntilDestroyed(this.destroyRef),
      filter(() => this.isDragging),
      tap(event => {
        if (this.cancelEvents()) {
          event.preventDefault();
        }
      }),
      tap(event => {
        const client = this.getClientDimensions(event);
        const deltaY = this.startDimensionsClient.height - client.height;
        const deltaX = this.startDimensionsClient.width - client.width;

        const newY = this.clampHeight(this.startDimensionsElement.height + this.signMul() * deltaY);
        const newX = this.clampWidth(this.startDimensionsElement.width + this.signMul() * deltaX);

        switch (this.resizeMode()) {
          case ResizeMode.Height:
            this.canvasElement.style.setProperty('height', `${newY}px`, "important");
            break;
          case ResizeMode.Width:
            this.canvasElement.style.setProperty('width', `${newX}px`, "important");
            break;
        }

      }),
    ).subscribe();

    const mouseUp$ = fromEvent(this.document, 'mouseup');
    const touchEnd$ = fromEvent<TouchEvent>(this.document, 'touchend');

    merge(mouseUp$, touchEnd$).pipe(
      takeUntilDestroyed(this.destroyRef),
      filter(() => this.isDragging),
      tap(() => {
        this.isDragging = false;
        if (this.interruptSelection()) {
          document.body.style.userSelect = '';
        }
      })
    ).subscribe();

    const mouseDown$ = fromEvent<MouseEvent>(this.el.nativeElement, 'mousedown');
    const touchStart$ = fromEvent<TouchEvent>(this.document, 'touchstart');

    merge(mouseDown$, touchStart$).pipe(
      takeUntilDestroyed(this.destroyRef),
      tap(event => this.startDrag(event)),
    ).subscribe();
  }

  private clampHeight(height: number): number {
    return Math.min(Math.max(height, this.minHeight()), this.maxHeight());
  }

  private clampWidth(width: number): number {
    return Math.min(Math.max(width, this.minWidth()), this.maxWidth());
  }

  private getClientDimensions(event: MouseEvent | TouchEvent): Dimensions {
    return {
      height: event instanceof MouseEvent ? event.clientY : event.touches[0].clientY,
      width: event instanceof MouseEvent ? event.clientX : event.touches[0].clientX,
    }
  }

  private getElementDimensions(): Dimensions {
    return {
      height: this.canvasElement.clientHeight,
      width: this.canvasElement.clientWidth,
    }
  }

  protected readonly ResizeMode = ResizeMode;
}
