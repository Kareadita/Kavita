import { ChangeDetectionStrategy, ChangeDetectorRef, Component, ElementRef, Input, OnChanges, OnDestroy, Renderer2, ViewChild } from '@angular/core';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { CoverUpdateEvent } from 'src/app/_models/events/cover-update-event';
import { ImageService } from 'src/app/_services/image.service';
import { EVENTS, MessageHubService } from 'src/app/_services/message-hub.service';

/**
 * This is used for images with placeholder fallback.
 */
@Component({
  selector: 'app-image',
  templateUrl: './image.component.html',
  styleUrls: ['./image.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ImageComponent implements OnChanges, OnDestroy {

  /**
   * Source url to load image
   */
  @Input() imageUrl!: string;
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

  @ViewChild('img', {static: true}) imgElem!: ElementRef<HTMLImageElement>;

  private readonly onDestroy = new Subject<void>();

  constructor(public imageService: ImageService, private renderer: Renderer2, private hubService: MessageHubService, private changeDetectionRef: ChangeDetectorRef) {
    this.hubService.messages$.pipe(takeUntil(this.onDestroy)).subscribe(res => {
      if (!this.processEvents) return;
      if (res.event === EVENTS.CoverUpdate) {
        const updateEvent = res.payload as CoverUpdateEvent;
        if (this.imageUrl === undefined || this.imageUrl === null || this.imageUrl === '') return;
        const enityType = this.imageService.getEntityTypeFromUrl(this.imageUrl);
        if (enityType === updateEvent.entityType) {
          const tokens = this.imageUrl.split('?')[1].split('&');

          //...seriesId=123&random=
          let id = tokens[0].replace(enityType + 'Id=', '');
          if (id.includes('&')) {
            id = id.split('&')[0];
          }
          if (id === (updateEvent.id + '')) {
            this.imageUrl = this.imageService.randomize(this.imageUrl);
            this.changeDetectionRef.markForCheck();
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
  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

}
