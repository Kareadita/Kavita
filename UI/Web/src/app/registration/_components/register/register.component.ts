import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
import { FormGroup, FormControl, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { AccountService } from 'src/app/_services/account.service';
import { MemberService } from 'src/app/_services/member.service';

/**
 * This is exclusivly used to register the first user on the server and nothing else
 */
@Component({
  selector: 'app-register',
  templateUrl: './register.component.html',
  styleUrls: ['./register.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RegisterComponent implements OnInit {

  registerForm: FormGroup = new FormGroup({
    email: new FormControl('', [Validators.email]),
    username: new FormControl('', [Validators.required]),
    password: new FormControl('', [Validators.required, Validators.maxLength(32), Validators.minLength(6)]),
  });

  constructor(private router: Router, private accountService: AccountService, 
    private toastr: ToastrService, private memberService: MemberService) {
    
      this.memberService.adminExists().pipe(take(1)).subscribe(adminExists => {
      if (adminExists) {
        this.router.navigateByUrl('login');
        return;
      }
    });
  }

  ngOnInit(): void {
  }

  submit() {
    const model = this.registerForm.getRawValue();
    this.accountService.register(model).subscribe((user) => {
      this.toastr.success('Account registration complete');
      this.router.navigateByUrl('login');
    });
  }
}
