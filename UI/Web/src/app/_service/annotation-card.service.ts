import {ApplicationRef, ComponentRef, createComponent, EmbeddedViewRef, inject, Injectable} from '@angular/core';
import {
  AnnotationCardComponent
} from '../book-reader/_components/_annotations/annotation-card/annotation-card.component';

@Injectable({
  providedIn: 'root'
})
export class AnnotationCardService {

  private readonly applicationRef = inject(ApplicationRef);

  private componentRef?: ComponentRef<AnnotationCardComponent>;

  show(config: {
    position: any;
    annotationText?: string;
    createdDate?: Date;
    onMouseEnter?: () => void;
    onMouseLeave?: () => void;
  }): ComponentRef<AnnotationCardComponent> {
    // Remove existing card if present
    this.hide();

    // Create component using createComponent (Angular 13+ approach)
    this.componentRef = createComponent(AnnotationCardComponent, {
      environmentInjector: this.applicationRef.injector
    });

    // Set inputs using signals
    this.componentRef.setInput('position', config.position);
    this.componentRef.setInput('annotationText', config.annotationText || 'This is test text');
    this.componentRef.setInput('createdDate', config.createdDate || new Date());

    // Set up event handlers
    if (config.onMouseEnter) {
      this.componentRef.instance.mouseEnter.subscribe(config.onMouseEnter);
    }
    if (config.onMouseLeave) {
      this.componentRef.instance.mouseLeave.subscribe(config.onMouseLeave);
    }

    // Attach to application
    this.applicationRef.attachView(this.componentRef.hostView);

    // Append to body
    const domElem = (this.componentRef.hostView as EmbeddedViewRef<any>).rootNodes[0] as HTMLElement;
    document.body.appendChild(domElem);

    return this.componentRef;
  }

  hide(): void {
    if (this.componentRef) {
      this.applicationRef.detachView(this.componentRef.hostView);
      this.componentRef.destroy();
      this.componentRef = undefined;
    }
  }

  updateHoverState(isHovered: boolean): void {
    if (this.componentRef) {
      this.componentRef.instance.isHovered.set(isHovered);
    }
  }
}
