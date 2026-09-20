import {ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {SettingItemComponent} from "../../../settings/_components/setting-item/setting-item.component";
import {AccountService} from "../../../_services/account.service";
import {AuthKey} from "../../../_models/user/auth-key";
import {UtcToLocalTimePipe} from "../../../_pipes/utc-to-local-time.pipe";
import {FormFieldDirective} from "../../../_directives/form-field.directive";
import {disabled, form, FormField, max, min, required, submit} from "@angular/forms/signals";
import {firstValueFrom} from "rxjs";

interface FormModel {
  name: string;
  keyLength: number;
  expiresUtc: string;
}

@Component({
  selector: 'app-create-auth-key',
  imports: [
    TranslocoDirective,
    SettingItemComponent,
    UtcToLocalTimePipe,
    FormFieldDirective,
    FormField
  ],
  templateUrl: './create-auth-key.component.html',
  styleUrl: './create-auth-key.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CreateAuthKeyComponent {

  private readonly modalRef = inject(NgbActiveModal);
  private readonly accountService = inject(AccountService);

  authKey = input<AuthKey | null>(null);
  isRotateFlow = computed(() => this.authKey() != null);

  private readonly formModel = signal<FormModel>({
    name: '', keyLength: 8, expiresUtc: ''
  });
  formGroup = form(this.formModel, p => {
    required(p.name);
    required(p.keyLength);
    min(p.keyLength, 8);
    max(p.keyLength, 32);

    disabled(p.name, {
      when: () => this.isRotateFlow()
    });
    disabled(p.keyLength, {
      when: () => this.isRotateFlow()
    });
    disabled(p.expiresUtc, {
      when: () => this.isRotateFlow()
    });
  });


  constructor() {
    effect(() => {
      const isRotateFlow = this.isRotateFlow();
      const authKey = this.authKey();
      if (!isRotateFlow || !authKey) return;

      untracked(() => {
        this.formGroup.name().value.set(authKey.name);
        this.formGroup.keyLength().value.set(authKey.key.length);
        this.formGroup.expiresUtc().value.set(authKey.expiresAtUtc);
      });
    });
  }


  close() {
    this.modalRef.dismiss();
  }

  async save() {
    const data = {...this.formModel()} as any;
    if (data.expiresUtc === '') {
      data.expiresUtc = null;
    }

    if (this.isRotateFlow()) {
      this.accountService.rotateAuthKey(this.authKey()!.id, data).subscribe(res => {
        this.modalRef.close(res);
      });
    } else {
      await submit(this.formGroup, async () => {
        try {
          const result = await firstValueFrom(this.accountService.createAuthKey(data));
          this.modalRef.close(result);
          return undefined;
        } catch {
          return [{fieldTree: this.formGroup.name, kind: 'duplicateName'}];
        }
      });
    }
  }
}
