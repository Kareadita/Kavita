import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { FormGroup, FormControl, Validators } from '@angular/forms';
import { take } from 'rxjs/operators';
import { AccountService } from 'src/app/_services/account.service';
import { SettingsService } from '../admin/settings.service';

@Component({
  selector: 'app-register-member',
  templateUrl: './register-member.component.html',
  styleUrls: ['./register-member.component.scss']
})
export class RegisterMemberComponent implements OnInit {

  @Input() firstTimeFlow = false;
  @Output() created = new EventEmitter<boolean>();

  adminExists = false;
  authDisabled: boolean = false;
  registerForm: FormGroup = new FormGroup({
      username: new FormControl('', [Validators.required]),
      password: new FormControl('', []),
      isAdmin: new FormControl(false, [])
  });
  errors: string[] = [];

  constructor(private accountService: AccountService, private settingsService: SettingsService) {
  }

  ngOnInit(): void {
    this.settingsService.getServerSettings().pipe(take(1)).subscribe(settings => {
      this.authDisabled = !settings.enableAuthentication;
    });
    if (this.firstTimeFlow) {
      this.registerForm.get('isAdmin')?.setValue(true);
    }
  }

  register() {
    this.accountService.register(this.registerForm.value).subscribe(resp => {
      this.created.emit(true);
    }, err => {
      this.errors = err;
    });
  }

  cancel() {
    this.created.emit(false);
  }

}
