import { AfterViewInit, Directive, ElementRef, TemplateRef, ViewContainerRef } from '@angular/core';

@Directive({
  selector: '[appShowIfScrollbar]'
})
export class ShowIfScrollbarDirective implements AfterViewInit {

  constructor(private el: ElementRef, private templateRef: TemplateRef<any>, private viewContainer: ViewContainerRef) { 
    
  }
  ngAfterViewInit(): void {
    // NOTE: This doesn't work!
    if (this.el.nativeElement.scrollHeight > this.el.nativeElement.clientHeight) {
      // If condition is true add template to DOM
      this.viewContainer.createEmbeddedView(this.templateRef);
    } else {
     // Else remove template from DOM
      this.viewContainer.clear();
    }
  }
  
}
