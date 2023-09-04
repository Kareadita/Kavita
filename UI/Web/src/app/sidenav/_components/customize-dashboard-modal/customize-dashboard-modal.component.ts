import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import {SafeHtmlPipe} from "../../../pipe/safe-html.pipe";
import {TranslocoDirective} from "@ngneat/transloco";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";

@Component({
  selector: 'app-customize-dashboard-modal',
  standalone: true,
  imports: [CommonModule, SafeHtmlPipe, TranslocoDirective],
  templateUrl: './customize-dashboard-modal.component.html',
  styleUrls: ['./customize-dashboard-modal.component.scss']
})
export class CustomizeDashboardModalComponent {

  constructor(public modal: NgbActiveModal) { }

  close() {
    this.modal.close();
  }

  save() {
    this.close();
  }

}
