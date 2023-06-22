import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import {AccountService} from "../../_services/account.service";
import {NgbModal} from "@ng-bootstrap/ng-bootstrap";
import {FeatureListModalComponent} from "../../_single-module/feature-list-modal/feature-list-modal.component";

@Component({
  selector: 'app-manage-kavitaplus',
  templateUrl: './manage-kavita-plus.component.html',
  styleUrls: ['./manage-kavita-plus.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageKavitaPlusComponent {
  constructor(private modalService: NgbModal) {}

  openFeatureListModal() {
    this.modalService.open(FeatureListModalComponent, {size: "lg"});
  }
}
