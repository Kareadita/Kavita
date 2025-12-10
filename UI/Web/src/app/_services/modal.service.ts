import {inject, Injectable, Type} from '@angular/core';
import {NgbModal, NgbModalOptions, NgbModalRef} from '@ng-bootstrap/ng-bootstrap';


@Injectable({
  providedIn: 'root'
})
export class ModalService {

  private modal = inject(NgbModal);

  open<T>(content: Type<T>, options?: NgbModalOptions): [NgbModalRef, T]  {
    const modal = this.modal.open(content, options);
    return [modal, modal.componentInstance as T]
  }

  hasOpenModals() {
    return this.modal.hasOpenModals()
  }

  get activeInstances() {
    return this.modal.activeInstances
  }

  dismissAll(reason?: any) {
    this.modal.dismissAll(reason);
  }

}
