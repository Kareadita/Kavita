import {ComponentRef, inject, Injectable, Type} from '@angular/core';
import {NgbOffcanvas, NgbOffcanvasOptions, NgbOffcanvasRef} from "@ng-bootstrap/ng-bootstrap";

export interface TypedOffcanvasRef<C> extends NgbOffcanvasRef {
  setInput<K extends string>(key: K, value: unknown): void;
}

@Injectable({
  providedIn: 'root',
})
export class DrawerService {

  private readonly offcanvas = inject(NgbOffcanvas);

  /** * TODO: This is a hack to get the ComponentRef because NgbOffcanvasRef does not expose it.
   * See https://github.com/ng-bootstrap/ng-bootstrap/issues/4688 */
  open<C>(content: Type<C>, options?: NgbOffcanvasOptions): TypedOffcanvasRef<C> {
    const ref = this.offcanvas.open(content, options) as TypedOffcanvasRef<C>;

    ref.setInput = (key: string, value: unknown) => {
      const componentRef: ComponentRef<C> = (ref as any)['_contentRef'].componentRef;
      componentRef.setInput(key, value);
    };

    return ref;
  }

  hasOpenOffcanvas() {
    return this.offcanvas.hasOpenOffcanvas();
  }

  dismiss() {
    this.offcanvas.dismiss();
  }

}
