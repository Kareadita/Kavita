import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import {AccountService} from "../../_services/account.service";

@Component({
  selector: 'app-user-kavitaplus',
  templateUrl: './user-kavita-plus.component.html',
  styleUrls: ['./user-kavita-plus.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserKavitaPlusComponent implements OnInit {

  hasValidLicense = false;
  private readonly accountService = inject(AccountService);
  private readonly cdRef = inject(ChangeDetectorRef);



  ngOnInit() {
    this.accountService.hasValidLicense().subscribe(res => {
      this.hasValidLicense = res;
      this.cdRef.markForCheck();
    })
  }

}
