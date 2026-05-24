import {ChangeDetectionStrategy, Component, computed, DestroyRef, inject, output} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators} from "@angular/forms";
import {WikiLink} from "../../../_models/wiki";
import {AccountService} from "../../../_services/account.service";
import {LicenseService} from "../../../_services/license.service";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {DiscordButtonComponent} from "../discord-button/discord-button.component";
import {TranslocoInjectComponent} from "../../../shared/_components/transloco-inject/transloco-inject.component";
import {TranslocoSlotDirective} from "../../../_directives/transloco-slot.directive";


export interface LicenseFormEvent {
  licenseKey: string,
  email: string,
  discordId: string | null,
  isValid: boolean
}

/**
 * This is the core form logic
 */
@Component({
  selector: 'app-edit-license-key',
  imports: [
    TranslocoDirective,
    FormsModule,
    ReactiveFormsModule,
    NgbTooltip,
    DiscordButtonComponent,
    TranslocoInjectComponent,
    TranslocoSlotDirective
  ],
  templateUrl: './edit-license-key.component.html',
  styleUrl: './edit-license-key.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EditLicenseKeyComponent {

  protected readonly accountService = inject(AccountService);
  protected readonly licenseService = inject(LicenseService);
  protected readonly destroyRef = inject(DestroyRef);

  /** This will show the additional connect with discord OAuth flow */
  updated = output<LicenseFormEvent>();


  /** This will trigger showing an additional helper to explain why the key is needed again */
  hasLicense = computed(() => this.licenseService.hasActiveLicense());
  showConnectWithDiscord = computed(() => {
    return !this.licenseService.licenseInfo()?.hasDiscordSet;
  });

  formGroup: FormGroup = new FormGroup({
    'licenseKey': new FormControl('', [Validators.required, Validators.maxLength(19),
      Validators.minLength(19), Validators.pattern(/^[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}$/)]),
    'email': new FormControl('', [Validators.required, Validators.email]),
    'discordId': new FormControl('', [Validators.pattern(/\d{18}/), Validators.maxLength(18),
      Validators.minLength(18)])
  });

  constructor() {

    const licenseInfo = this.licenseService.licenseInfo();
    if (licenseInfo) {
      this.formGroup.get('email')?.setValue(licenseInfo.registeredEmail);
      this.formGroup.get('discordId')?.setValue(licenseInfo.discordId);
    }

    this.formGroup.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(change => {
      this.updated.emit({...this.formGroup.value, isValid: this.formGroup.valid});
    });
  }

  protected readonly WikiLink = WikiLink;
}
