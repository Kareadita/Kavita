import { Component, Input, OnInit } from '@angular/core';
import { NgbActiveModal, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { UpdateVersionEvent } from 'src/app/_models/events/update-version-event';



@Component({
  selector: 'app-update-notification-modal',
  templateUrl: './update-notification-modal.component.html',
  styleUrls: ['./update-notification-modal.component.scss']
})
export class UpdateNotificationModalComponent implements OnInit {

  @Input() updateData!: UpdateVersionEvent;

  constructor(private modalService: NgbModal, public modal: NgbActiveModal) { }

  ngOnInit(): void {
  }

  close() {
    this.modal.close({success: false, series: undefined});
  }

}
