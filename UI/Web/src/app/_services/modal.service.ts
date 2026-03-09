import {ComponentRef, inject, Injectable, Type} from '@angular/core';
import {NgbModal, NgbModalOptions, NgbModalRef} from '@ng-bootstrap/ng-bootstrap';

export interface TypedModalRef<C> extends NgbModalRef {
  setInput<K extends string>(key: K, value: unknown): void;
}

@Injectable({
  providedIn: 'root'
})
export class ModalService {

  private modal = inject(NgbModal);

  /** * TODO: This is a hack to get the ComponentRef because NgbModalRef does not expose it.
   * See https://github.com/ng-bootstrap/ng-bootstrap/issues/4688 */
  open<C>(content: Type<C>, options?: NgbModalOptions): TypedModalRef<C> {
    const ref = this.modal.open(content, options) as TypedModalRef<C>;

    ref.setInput = (key: string, value: unknown) => {
      const componentRef: ComponentRef<C> = (ref as any)['_contentRef'].componentRef;
      componentRef.setInput(key, value);
    };

    return ref;
  }
}
