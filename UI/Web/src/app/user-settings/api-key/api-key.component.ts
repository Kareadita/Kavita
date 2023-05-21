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

@Component({
  selector: 'app-api-key',
  templateUrl: './api-key.component.html',
  styleUrls: ['./api-key.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ApiKeyComponent implements OnInit {

  @Input() title: string = 'API Key';
  @Input() showRefresh: boolean = true;
  @Input() transform: (val: string) => string = (val: string) => val;
  @Input() tooltipText: string = '';
  @ViewChild('apiKey') inputElem!: ElementRef;
  key: string = '';
  private readonly destroyRef = inject(DestroyRef);


  constructor(private confirmService: ConfirmService, private accountService: AccountService, private toastr: ToastrService, private clipboard: Clipboard,
              private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(user => {
      let key = '';
      if (user) {
        key = user.apiKey;
      } else {
        key = 'ERROR - KEY NOT SET';
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
    if (!await this.confirmService.confirm('This will invalidate any OPDS configurations you have setup. Are you sure you want to continue?')) {
      return;
    }
    this.accountService.resetApiKey().subscribe(newKey => {
      this.key = newKey;
      this.cdRef.markForCheck();
      this.toastr.success('API Key reset');
    });
  }

  selectAll() {
    if (this.inputElem) {
      this.inputElem.nativeElement.setSelectionRange(0, this.key.length);
      this.cdRef.markForCheck();
    }
  }

}
