import { noUndefined } from '@angular/compiler/src/util';
import { Component, ElementRef, Input, OnInit, ViewChild } from '@angular/core';
import { take } from 'rxjs/operators';
import { ConfirmService } from 'src/app/shared/confirm.service';
import { AccountService } from 'src/app/_services/account.service';

@Component({
  selector: 'app-api-key',
  templateUrl: './api-key.component.html',
  styleUrls: ['./api-key.component.scss']
})
export class ApiKeyComponent implements OnInit {

  @Input() title: string = 'API Key';
  @Input() showRefresh: boolean = true;
  @Input() transform: (val: string) => string = (val: string) => val;
  @Input() tooltipText: string = '';
  @ViewChild('apiKey') inputElem!: ElementRef;
  key: string = '';
  

  constructor(private confirmService: ConfirmService, private accountService: AccountService) { }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      let key = '';
      if (user) {
        key = user.apiKey;
      } else {
        key = 'ERROR - KEY NOT SET';
      }

      if (this.transform != undefined) {
        this.key = this.transform(key);
      }
    });
  }

  async copy() {
    await navigator.clipboard.writeText(this.key);
  }

  async refresh() {
    if (!await this.confirmService.confirm('This will invalidate any OPDS configurations you have setup. Are you sure you want to continue?')) {
      return;
    }


  }

  selectAll() {
    if (this.inputElem) {
      this.inputElem.nativeElement.setSelectionRange(0, this.key.length);
    }
  }

}
