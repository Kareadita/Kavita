import { Component, ElementRef, Input, OnChanges, OnInit, Renderer2, SimpleChanges, ViewChild } from '@angular/core';
import { ImageService } from 'src/app/_services/image.service';

/**
 * This is used for images with placeholder fallback.
 */
@Component({
  selector: 'app-image',
  templateUrl: './image.component.html',
  styleUrls: ['./image.component.scss']
})
export class ImageComponent implements OnChanges {

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

  @ViewChild('img', {static: true}) imgElem!: ElementRef<HTMLImageElement>;

  constructor(public imageService: ImageService, private renderer: Renderer2) { }

  ngOnChanges(changes: SimpleChanges): void {
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
    
  }

}
