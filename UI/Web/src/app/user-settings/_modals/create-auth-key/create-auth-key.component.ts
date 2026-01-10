import {ChangeDetectionStrategy, Component, computed, inject, OnInit, signal} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {SettingItemComponent} from "../../../settings/_components/setting-item/setting-item.component";
import {DatePipe} from "@angular/common";
import {AccountService} from "../../../_services/account.service";
import {AuthKey} from "../../../_models/user/auth-key";

@Component({
  selector: 'app-create-auth-key',
  imports: [
    TranslocoDirective,
    ReactiveFormsModule,
    SettingItemComponent,
    DatePipe
  ],
  templateUrl: './create-auth-key.component.html',
  styleUrl: './create-auth-key.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CreateAuthKeyComponent implements OnInit {

  private readonly modalRef = inject(NgbActiveModal);
  private readonly accountService = inject(AccountService);

  authKey = signal<AuthKey | null>(null);
  isRotateFlow = computed(() => this.authKey() != null);

  settingsForm: FormGroup = new FormGroup({
    name: new FormControl('', [Validators.required]),
    keyLength: new FormControl(8, [Validators.required, Validators.min(8), Validators.max(32)]),
    expiresUtc: new FormControl('', []),
  });

  ngOnInit() {
    const authKey = this.authKey();

    if (this.isRotateFlow() && authKey) {
      this.settingsForm.get('name')?.disable();
      this.settingsForm.get('name')?.setValue(authKey.name);
      this.settingsForm.get('keyLength')?.setValue(authKey.key.length);
      this.settingsForm.get('expiresUtc')?.disable();
      this.settingsForm.get('expiresUtc')?.setValue(authKey.expiresAtUtc);
    }
  }


  close() {
    this.modalRef.dismiss();
  }

  save() {
    const data = this.settingsForm.value;
    if (data.expiresUtc === '') {
      data.expiresUtc = null;
    }

    if (this.isRotateFlow()) {
      this.accountService.rotateAuthKey(this.authKey()!.id, {...data, name: this.authKey()!.name}).subscribe(res => {
        this.modalRef.close(res);
      });
    } else {
      this.accountService.createAuthKey(data).subscribe(res => {
        this.modalRef.close(res);
      });
    }
  }
}
