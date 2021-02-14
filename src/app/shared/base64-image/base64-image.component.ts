import { Component, Input, OnInit } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';

@Component({
  selector: 'app-base64-image',
  templateUrl: './base64-image.component.html',
  styleUrls: ['./base64-image.component.scss']
})
export class Base64ImageComponent implements OnInit {

  @Input() src!: string;
  @Input() alt = '';
  @Input() style = {};
  safeImage: any;
  placeholderImage = 'assets/images/image-placeholder.jpg';


  constructor(private sanitizer: DomSanitizer) { }

  ngOnInit(): void {
    this.createSafeImage();
  }

  createSafeImage() {
    if (!this.isNullOrEmpty(this.src)) {
      try{
        this.safeImage = this.sanitizer.bypassSecurityTrustUrl('data:image/jpeg;base64,' + this.src);
        return;
      } catch (e) {}
    }
    this.safeImage = this.sanitizer.bypassSecurityTrustUrl(this.placeholderImage);
  }

  isNullOrEmpty(val: string) {
    return val === null || val === undefined || val === '';
  }

}
