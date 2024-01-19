import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";

@Component({
  selector: 'app-update-modal',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './update-modal.component.html',
  styleUrls: ['./update-modal.component.scss']
})
export class UpdateModalComponent {

  constructor(private ngbActiveModal: NgbActiveModal) {
    console.log('hello')
  }
  close() {
    this.ngbActiveModal.close();
  }
}
