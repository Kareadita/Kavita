import {ChangeDetectionStrategy, Component, Input, OnInit} from '@angular/core';
import {NgbActiveModal, NgbModalModule} from '@ng-bootstrap/ng-bootstrap';
import { UpdateVersionEvent } from 'src/app/_models/events/update-version-event';
import {CommonModule} from "@angular/common";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {TranslocoDirective} from "@jsverse/transloco";
import {WikiLink} from "../../_models/wiki";


@Component({
  selector: 'app-update-notification-modal',
  standalone: true,
  imports: [CommonModule, NgbModalModule, SafeHtmlPipe, TranslocoDirective],
  templateUrl: './update-notification-modal.component.html',
  styleUrls: ['./update-notification-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UpdateNotificationModalComponent implements OnInit {

  @Input({required: true}) updateData!: UpdateVersionEvent;
  updateUrl: string = WikiLink.UpdateNative;

  constructor(public modal: NgbActiveModal) { }

  ngOnInit() {
    if (this.updateData.isDocker) {
      this.updateUrl = WikiLink.UpdateDocker;
    } else {
      this.updateUrl = WikiLink.UpdateNative;
    }
  }

  close() {
    this.modal.close({success: false, series: undefined});
  }
}
