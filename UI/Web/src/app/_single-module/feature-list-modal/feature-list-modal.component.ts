import {ChangeDetectionStrategy, Component} from '@angular/core';
import {CommonModule} from '@angular/common';
import {PipeModule} from "../../pipe/pipe.module";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";

@Component({
  selector: 'app-feature-list-modal',
  standalone: true,
  imports: [CommonModule, PipeModule],
  templateUrl: './feature-list-modal.component.html',
  styleUrls: ['./feature-list-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FeatureListModalComponent {

  constructor(private modal: NgbActiveModal) {}

  close() {
    this.modal.close();
  }
}
