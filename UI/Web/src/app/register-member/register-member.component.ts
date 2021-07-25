import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { FormGroup, FormControl, Validators } from '@angular/forms';
import { AccountService } from 'src/app/_services/account.service';

@Component({
  selector: 'app-register-member',
  templateUrl: './register-member.component.html',
  styleUrls: ['./register-member.component.scss']
})
export class RegisterMemberComponent implements OnInit {

  @Input() firstTimeFlow = false;
  @Output() created = new EventEmitter<boolean>();

  adminExists = false;
  registerForm: FormGroup = new FormGroup({
      username: new FormControl('', [Validators.required]),
      password: new FormControl('', [Validators.required]),
      isAdmin: new FormControl(false, [])
  });
  errors: string[] = [];

  constructor(private accountService: AccountService) {
  }

  ngOnInit(): void {
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
