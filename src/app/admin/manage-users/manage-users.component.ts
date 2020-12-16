import { Component, OnInit, ViewChild } from '@angular/core';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { DirectoryPickerComponent, DirectoryPickerResult } from 'src/app/directory-picker/directory-picker.component';
import { MemberService } from 'src/app/member.service';
import { Member } from 'src/app/_models/member';

@Component({
  selector: 'app-manage-users',
  templateUrl: './manage-users.component.html',
  styleUrls: ['./manage-users.component.scss']
})
export class ManageUsersComponent implements OnInit {

  members: Member[] = [];
  closeResult = ''; // Debug code
  @ViewChild('content') content: any;

  constructor(private memberService: MemberService) { }

  ngOnInit(): void {
    console.log('User Component');
    this.memberService.getMembers().subscribe(members => {
      this.members = members;
    });
  }

  
}
