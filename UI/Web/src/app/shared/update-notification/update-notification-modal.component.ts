import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import {NgbActiveModal, NgbModalModule} from '@ng-bootstrap/ng-bootstrap';
import { UpdateVersionEvent } from 'src/app/_models/events/update-version-event';
import {CommonModule} from "@angular/common";
import {PipeModule} from "../../pipe/pipe.module";



@Component({
  selector: 'app-update-notification-modal',
  standalone: true,
  imports: [CommonModule, PipeModule, NgbModalModule],
  templateUrl: './update-notification-modal.component.html',
  styleUrls: ['./update-notification-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UpdateNotificationModalComponent {

  @Input({required: true}) updateData!: UpdateVersionEvent;

  constructor(public modal: NgbActiveModal) { }

  close() {
    this.modal.close({success: false, series: undefined});
  }
}
