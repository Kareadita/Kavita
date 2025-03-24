import {ChangeDetectionStrategy, Component, Input, OnInit} from '@angular/core';
import {NgbActiveModal, NgbModalModule} from '@ng-bootstrap/ng-bootstrap';
import {UpdateVersionEvent} from 'src/app/_models/events/update-version-event';
import {CommonModule} from "@angular/common";
import {TranslocoDirective} from "@jsverse/transloco";
import {WikiLink} from "../../../_models/wiki";
import {ChangelogUpdateItemComponent} from "../changelog-update-item/changelog-update-item.component";


@Component({
  selector: 'app-update-notification-modal',
  imports: [CommonModule, NgbModalModule, TranslocoDirective, ChangelogUpdateItemComponent],
  templateUrl: './update-notification-modal.component.html',
  styleUrls: ['./update-notification-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UpdateNotificationModalComponent implements OnInit {

  @Input({required: true}) updateData!: UpdateVersionEvent;
  updateUrl: string = WikiLink.UpdateNative;

  // TODO: I think I can remove this and just use NewUpdateModalComponent instead which handles both Nightly/Stable

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
