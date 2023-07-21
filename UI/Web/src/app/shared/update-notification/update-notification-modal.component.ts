import {ChangeDetectionStrategy, Component, Input, OnInit} from '@angular/core';
import {NgbActiveModal, NgbModalModule} from '@ng-bootstrap/ng-bootstrap';
import { UpdateVersionEvent } from 'src/app/_models/events/update-version-event';
import {CommonModule} from "@angular/common";
import {SafeHtmlPipe} from "../../pipe/safe-html.pipe";



@Component({
  selector: 'app-update-notification-modal',
  standalone: true,
  imports: [CommonModule, NgbModalModule, SafeHtmlPipe],
  templateUrl: './update-notification-modal.component.html',
  styleUrls: ['./update-notification-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UpdateNotificationModalComponent implements OnInit {

  @Input({required: true}) updateData!: UpdateVersionEvent;
  updateUrl: string = 'https://wiki.kavitareader.com/en/install/windows-install#updating-kavita';

  constructor(public modal: NgbActiveModal) { }

  ngOnInit() {
    if (this.updateData.isDocker) {
      this.updateUrl = 'https://wiki.kavitareader.com/en/install/docker-install#updating-kavita';
    } else {
      this.updateUrl = 'https://wiki.kavitareader.com/en/install/windows-install#updating-kavita';
    }
  }

  close() {
    this.modal.close({success: false, series: undefined});
  }
}
