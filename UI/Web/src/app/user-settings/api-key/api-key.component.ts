import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  ElementRef, inject,
  Input,
  OnInit,
  ViewChild
} from '@angular/core';
import {ToastrService} from 'ngx-toastr';
import {ConfirmService} from 'src/app/shared/confirm.service';
import {AccountService} from 'src/app/_services/account.service';
import {Clipboard} from '@angular/cdk/clipboard';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { NgbTooltip } from '@ng-bootstrap/ng-bootstrap';
import { NgIf } from '@angular/common';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";

@Component({
    selector: 'app-api-key',
    templateUrl: './api-key.component.html',
    styleUrls: ['./api-key.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [NgbTooltip, TranslocoDirective, SettingItemComponent]
})
export class ApiKeyComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);
  private readonly confirmService = inject(ConfirmService);
  private readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);
  private readonly clipboard = inject(Clipboard);
  private readonly cdRef = inject(ChangeDetectorRef);

  @Input() title: string = 'API Key';
  @Input() showRefresh: boolean = true;
  @Input() transform: (val: string) => string = (val: string) => val;
  @Input() tooltipText: string = '';
  @Input() hideData = true;
  @ViewChild('apiKey') inputElem!: ElementRef;

  key: string = '';
  isDataHidden: boolean = this.hideData;

  get InputType() {
    return (this.hideData && this.isDataHidden) ? 'password' : 'text';
  }


  ngOnInit(): void {
    this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(user => {
      let key = '';
      if (user) {
        key = user.apiKey;
      } else {
        key = translate('api-key.no-key');
      }

      if (this.showRefresh) {
        this.showRefresh = !this.accountService.hasReadOnlyRole(user!);
      }

      if (this.transform != undefined) {
        this.key = this.transform(key);
        this.cdRef.markForCheck();
      }
    });
  }

  async copy() {
    this.inputElem.nativeElement.select();
    this.clipboard.copy(this.inputElem.nativeElement.value);
    this.inputElem.nativeElement.setSelectionRange(0, 0);
    this.cdRef.markForCheck();
  }

  async refresh() {
    if (!await this.confirmService.confirm(translate('api-key.confirm-reset'))) {
      return;
    }
    this.accountService.resetApiKey().subscribe(newKey => {
      this.key = newKey;
      this.cdRef.markForCheck();
      this.toastr.success(translate('api-key.key-reset'));
    });
  }

  selectAll() {
    if (this.inputElem) {
      this.inputElem.nativeElement.setSelectionRange(0, this.key.length);
      this.cdRef.markForCheck();
    }
  }

  toggleVisibility() {
    this.isDataHidden = !this.isDataHidden;
    this.cdRef.markForCheck();
  }

}
