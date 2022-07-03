import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { UpdateVersionEvent } from 'src/app/_models/events/update-version-event';



@Component({
  selector: 'app-update-notification-modal',
  templateUrl: './update-notification-modal.component.html',
  styleUrls: ['./update-notification-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UpdateNotificationModalComponent {

  @Input() updateData!: UpdateVersionEvent;

  constructor(public modal: NgbActiveModal) { }

  close() {
    this.modal.close({success: false, series: undefined});
  }
}
