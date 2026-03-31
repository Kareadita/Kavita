import {ComponentRef, inject, Injectable, InputSignal, ModelSignal, Type, WritableSignal} from '@angular/core';
import {NgbModal, NgbModalOptions, NgbModalRef} from '@ng-bootstrap/ng-bootstrap';
import {environment} from "src/environments/environment";

export type UnwrapSignal<T> =
  T extends ModelSignal<infer R> ? R :
  T extends InputSignal<infer R> ? R :
  T;

export interface TypedModalRef<C> extends NgbModalRef {
  setInput<K extends keyof C>(key: K, value: UnwrapSignal<C[K]>): void;
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

    ref.setInput = <K extends keyof C>(key: K, value: UnwrapSignal<C[K]>) => {
      const componentRef: ComponentRef<C> = (ref as any)['_contentRef']?.componentRef;

      if (componentRef) {
        componentRef.setInput(key as string, value);
        return;
      }

      // Throw an error in development, so we're sure to catch these issues
      if (!environment.production) {
        throw new Error('ModalService.setInput: componentRef is not available; input "' + String(key) + '" was not set.');
      }

      console.warn('ModalService.setInput: componentRef is not available; input "' + String(key) + '" was not set.');
    };

    return ref;
  }
}
