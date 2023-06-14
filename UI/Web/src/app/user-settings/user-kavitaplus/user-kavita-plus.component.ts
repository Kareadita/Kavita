import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import {AccountService} from "../../_services/account.service";
import {NgbModal} from "@ng-bootstrap/ng-bootstrap";
import {FeatureListModalComponent} from "../../_single-module/feature-list-modal/feature-list-modal.component";

@Component({
  selector: 'app-user-kavitaplus',
  templateUrl: './user-kavita-plus.component.html',
  styleUrls: ['./user-kavita-plus.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserKavitaPlusComponent implements OnInit {

  hasValidLicense = false;
  private readonly cdRef = inject(ChangeDetectorRef);

  constructor(private accountService: AccountService, private modalService: NgbModal) {
  }

  ngOnInit() {
    this.accountService.hasValidLicense().subscribe(res => {
      this.hasValidLicense = res;
      this.cdRef.markForCheck();
    })
  }

  validateLicense() {
    this.accountService.hasValidLicense(true).subscribe(res => {
      this.hasValidLicense = res;
      this.cdRef.markForCheck();
    });
  }

  openFeatureListModal() {
    this.modalService.open(FeatureListModalComponent, {size: "lg"});
  }


}
