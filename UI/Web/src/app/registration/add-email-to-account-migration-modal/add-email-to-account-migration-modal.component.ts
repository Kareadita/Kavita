import { Component, Input, OnInit } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { SafeUrl } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { ConfirmService } from 'src/app/shared/confirm.service';
import { AccountService } from 'src/app/_services/account.service';
import { MemberService } from 'src/app/_services/member.service';
import { ServerService } from 'src/app/_services/server.service';

@Component({
  selector: 'app-add-email-to-account-migration-modal',
  templateUrl: './add-email-to-account-migration-modal.component.html',
  styleUrls: ['./add-email-to-account-migration-modal.component.scss']
})
export class AddEmailToAccountMigrationModalComponent implements OnInit {

  @Input() username!: string;
  @Input() password!: string;

  isSaving: boolean = false;
  registerForm: FormGroup = new FormGroup({});
  emailLink: string = '';
  emailLinkUrl: SafeUrl | undefined;

  constructor(private accountService: AccountService, private modal: NgbActiveModal, 
    private serverService: ServerService, private confirmService: ConfirmService) {
  }

  ngOnInit(): void {
    this.registerForm.addControl('username', new FormControl(this.username, [Validators.required]));
    this.registerForm.addControl('email', new FormControl('', [Validators.required, Validators.email]));
    this.registerForm.addControl('password', new FormControl(this.password, [Validators.required]));
  }

  close() {
    this.modal.close(false);
  }

  save() {
    this.serverService.isServerAccessible().subscribe(canAccess => {
      const model = this.registerForm.getRawValue();
      model.sendEmail = canAccess;
      this.accountService.migrateUser(model).subscribe(async (email) => {
        if (!canAccess) {
          // Display the email to the user
          this.emailLink = email;
          await this.confirmService.alert('Please click this link to confirm your email. You must confirm to be able to login. You may need to log out of the current account before clicking. <br/> <a href="' + this.emailLink + '" target="_blank">' + this.emailLink + '</a>');
          this.modal.close(true);
        } else {
          await this.confirmService.alert('Please check your email for the confirmation link. You must confirm to be able to login.');
          this.modal.close(true);
        }
      });
    });
    
  }

  

}
