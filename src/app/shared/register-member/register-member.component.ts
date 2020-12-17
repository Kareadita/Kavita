import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { FormGroup, FormControl, Validators } from '@angular/forms';
import { MemberService } from 'src/app/_services/member.service';
import { Member } from 'src/app/_models/member';
import { AccountService } from 'src/app/_services/account.service';

@Component({
  selector: 'app-register-member',
  templateUrl: './register-member.component.html',
  styleUrls: ['./register-member.component.scss']
})
export class RegisterMemberComponent implements OnInit {

  @Output() created = new EventEmitter<boolean>();

  adminExists = false;
  model: any = {};
  registerForm: FormGroup = new FormGroup({
      username: new FormControl('', [Validators.required]),
      password: new FormControl('', [Validators.required]),
      isAdmin: new FormControl(false, [])
  });

  constructor(private accountService: AccountService, private memberService: MemberService) { 
    this.memberService.getMembers().subscribe(members => {
      this.adminExists = members.filter((m: Member) => m.isAdmin).length > 0;
      if (!this.adminExists) {
        this.registerForm.get('isAdmin')?.setValue(true);
        this.model.isAdmin = true;
      }
    });
  }

  ngOnInit(): void {
  }

  register() {
    this.model.username = this.registerForm.get('username')?.value;
    this.model.password = this.registerForm.get('password')?.value;
    this.model.isAdmin = this.registerForm.get('isAdmin')?.value;

    this.accountService.register(this.model).subscribe(resp => {
      this.created.emit(true);
    }, err => {
      console.log('validation errors from interceptor', err);
    });
  }

  cancel() {
    this.created.emit(false);
  }

}
