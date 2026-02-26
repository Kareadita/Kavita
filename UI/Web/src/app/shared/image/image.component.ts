import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  input,
  model,
  output,
  Renderer2,
  RendererStyleFlags2,
  viewChild
} from '@angular/core';
import {CoverUpdateEvent} from 'src/app/_models/events/cover-update-event';
import {ImageService} from 'src/app/_services/image.service';
import {EVENTS, MessageHubService} from 'src/app/_services/message-hub.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {LazyLoadImageModule, StateChange} from "ng-lazyload-image";

/**
 * This is used for images with placeholder fallback.
 */
@Component({
    selector: 'app-image',
    imports: [LazyLoadImageModule],
    templateUrl: './image.component.html',
    styleUrls: ['./image.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ImageComponent {

  private readonly destroyRef = inject(DestroyRef);
  protected readonly imageService = inject(ImageService);
  private readonly renderer = inject(Renderer2);
  private readonly hubService = inject(MessageHubService);

  /**
   * Source url to load image
   */
  readonly imageUrl = model.required<string>();
  /**
   * Width of the image. If not defined, will not be applied
   */
  readonly width = input('');
  /**
   * Height of the image. If not defined, will not be applied
   */
  readonly height = input('');
  /**
   * If the image component should respond to cover updates
   */
  readonly processEvents = input(true);
  /**
   * Note: Parent component must use ViewEncapsulation.None
   */
  readonly classes = input('');
  /**
   * A collection of styles to apply. This is useful if the parent component doesn't want to use no view encapsulation
   */
  readonly styles = input<{[key: string]: string}>({});
  readonly errorImage = input(this.imageService.errorImage);
  /**
   * If the image load fails, instead of showing an error image, hide the image (visibility)
   */
  readonly hideOnError = input(false);
  /**
   * Sets the object-fit property of the image. Default is 'fill'.
   */
  readonly objectFit = input<'fill' | 'contain' | 'cover' | 'none' | 'scale-down'>('fill');

  readonly imgElem = viewChild.required<ElementRef<HTMLImageElement>>('img');
  /**
   * Outputs when the image failed to load
   */
  readonly errorLoad = output<string>();

  constructor() {
    this.hubService.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(res => {
      if (!this.processEvents()) return;
      if (res.event === EVENTS.CoverUpdate) {
        const updateEvent = res.payload as CoverUpdateEvent;
        const url = this.imageUrl();
        if (url === undefined || url === null || url === '') return;
        const entityType = this.imageService.getEntityTypeFromUrl(url);
        if (entityType === updateEvent.entityType) {
          const tokens = url.split('?')[1].split('&');

          //...seriesId=123&random=
          let id = tokens[0].replace(entityType + 'Id=', '');
          if (id.includes('&')) {
            id = id.split('&')[0];
          }
          if (id === (updateEvent.id + '')) {
            this.imageUrl.set(this.imageService.randomize(url));
          }
        }
      }
    });

    effect(() => {
      const elem = this.imgElem().nativeElement;
      const width = this.width();
      const height = this.height();
      const styles = this.styles();
      const classes = this.classes();

      if (width !== '') {
        this.renderer.setStyle(elem, 'width', width);
      }

      if (height !== '') {
        this.renderer.setStyle(elem, 'height', height);
      }

      const styleKeys = Object.keys(styles);
      if (styleKeys.length !== 0) {
        styleKeys.forEach(key => {
          this.renderer.setStyle(elem, key, styles[key], RendererStyleFlags2.Important);
        });
      }

      if (classes !== '') {
        const classTokens = classes.split(' ');
        classTokens.forEach(cls => this.renderer.addClass(elem, cls));
      }
    });
  }


  myCallbackFunction(event: StateChange) {
    const image = this.imgElem().nativeElement;
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
        this.renderer.removeClass(image, 'fade-in');
        if (this.hideOnError()) {
          this.renderer.addClass(image, 'd-none');
        }
        this.errorLoad.emit(this.imageUrl());

        break;
      case 'finally':
        // The last event before cleaning up
        break;
    }
  }

}
