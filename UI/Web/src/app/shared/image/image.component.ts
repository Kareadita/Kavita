import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  ElementRef,
  inject,
  Input,
  OnChanges,
  Renderer2,
  ViewChild
} from '@angular/core';
import { CoverUpdateEvent } from 'src/app/_models/events/cover-update-event';
import { ImageService } from 'src/app/_services/image.service';
import { EVENTS, MessageHubService } from 'src/app/_services/message-hub.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {CommonModule, NgOptimizedImage} from "@angular/common";
import {LazyLoadImageModule, StateChange} from "ng-lazyload-image";

/**
 * This is used for images with placeholder fallback.
 */
@Component({
  selector: 'app-image',
  standalone: true,
  imports: [CommonModule, NgOptimizedImage, LazyLoadImageModule],
  templateUrl: './image.component.html',
  styleUrls: ['./image.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ImageComponent implements OnChanges {

  private readonly destroyRef = inject(DestroyRef);
  protected readonly imageService = inject(ImageService);
  private readonly renderer = inject(Renderer2);
  private readonly hubService = inject(MessageHubService);
  private readonly cdRef = inject(ChangeDetectorRef);

  /**
   * Source url to load image
   */
  @Input({required: true}) imageUrl!: string;
  /**
   * Width of the image. If not defined, will not be applied
   */
  @Input() width: string = '';
  /**
   * Height of the image. If not defined, will not be applied
   */
  @Input() height: string = '';
  /**
   * Max Width of the image. If not defined, will not be applied
   */
  @Input() maxWidth: string = '';
  /**
   * Max Height of the image. If not defined, will not be applied
   */
   @Input() maxHeight: string = '';
  /**
   * Border Radius of the image. If not defined, will not be applied
   */
   @Input() borderRadius: string = '';
     /**
   * Object fit of the image. If not defined, will not be applied
   */
   @Input() objectFit: string = '';
  /**
   * Background of the image. If not defined, will not be applied
   */
   @Input() background: string = '';
   /**
    * If the image component should respond to cover updates
    */
   @Input() processEvents: boolean = true;
  /**
   * Note: Parent component must use ViewEncapsulation.None
   */
  @Input() classes: string = '';
  /**
   * A collection of styles to apply. This is useful if the parent component doesn't want to use no view encapsulation
   */
  @Input() styles: string = '';
  @Input() errorImage: string = this.imageService.errorImage;

  @ViewChild('img', {static: true}) imgElem!: ElementRef<HTMLImageElement>;


  constructor() {
    this.hubService.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(res => {
      if (!this.processEvents) return;
      if (res.event === EVENTS.CoverUpdate) {
        const updateEvent = res.payload as CoverUpdateEvent;
        if (this.imageUrl === undefined || this.imageUrl === null || this.imageUrl === '') return;
        const entityType = this.imageService.getEntityTypeFromUrl(this.imageUrl);
        if (entityType === updateEvent.entityType) {
          const tokens = this.imageUrl.split('?')[1].split('&');

          //...seriesId=123&random=
          let id = tokens[0].replace(entityType + 'Id=', '');
          if (id.includes('&')) {
            id = id.split('&')[0];
          }
          if (id === (updateEvent.id + '')) {
            this.imageUrl = this.imageService.randomize(this.imageUrl);
            this.cdRef.markForCheck();
          }
        }
      }
    });
  }

  ngOnChanges(): void {
    if (this.width != '') {
      this.renderer.setStyle(this.imgElem.nativeElement, 'width', this.width);
    }

    if (this.height != '') {
      this.renderer.setStyle(this.imgElem.nativeElement, 'height', this.height);
    }

    if (this.maxWidth != '') {
      this.renderer.setStyle(this.imgElem.nativeElement, 'max-width', this.maxWidth);
    }

    if (this.maxHeight != '') {
      this.renderer.setStyle(this.imgElem.nativeElement, 'max-height', this.maxHeight);
    }

    if (this.borderRadius != '') {
      this.renderer.setStyle(this.imgElem.nativeElement, 'border-radius', this.borderRadius);
    }

    if (this.objectFit != '') {
      this.renderer.setStyle(this.imgElem.nativeElement, 'object-fit', this.objectFit);
    }

    if (this.background != '') {
      this.renderer.setStyle(this.imgElem.nativeElement, 'background', this.background);
    }

    if (this.styles != '') {
      this.renderer.setStyle(this.imgElem.nativeElement, 'styles', this.styles);
    }

    if (this.classes != '') {
      this.renderer.addClass(this.imgElem.nativeElement, this.classes);
    }
  }


  myCallbackFunction(event: StateChange) {
    const image = this.imgElem.nativeElement;
    switch (event.reason) {
      case 'setup':
        // The lib has been instantiated but we have not done anything yet.
        break;
      case 'observer-emit':
        // The image observer (intersection/scroll/custom observer) has emit a value so we
        // should check if the image is in the viewport.
        // `event.data` is the event in this case.
        break;
      case 'start-loading':
        // The image is in the viewport so the image will start loading
        break;
      case 'mount-image':
        // The image has been loaded successfully so lets put it into the DOM
        break;
      case 'loading-succeeded':
        // The image has successfully been loaded and placed into the DOM
        this.renderer.addClass(image, 'loaded');
        break;
      case 'loading-failed':
        // The image could not be loaded for some reason.
        // `event.data` is the error in this case
        image.src = this.errorImage;
        this.cdRef.markForCheck();
        break;
      case 'finally':
        // The last event before cleaning up
        break;
    }
  }

}
