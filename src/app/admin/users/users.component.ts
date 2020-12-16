import { Component, OnInit, ViewChild } from '@angular/core';
import { ModalDismissReasons, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { DirectoryPickerComponent, DirectoryPickerResult } from 'src/app/directory-picker/directory-picker.component';
import { MemberService } from 'src/app/member.service';
import { Member } from 'src/app/_models/member';

@Component({
  selector: 'app-users',
  templateUrl: './users.component.html',
  styleUrls: ['./users.component.scss']
})
export class UsersComponent implements OnInit {

  members: Member[] = [];
  closeResult = ''; // Debug code
  @ViewChild('content') content: any;

  constructor(private memberService: MemberService, private modalService: NgbModal) { }

  ngOnInit(): void {
    console.log('User Component');
    this.memberService.getMembers().subscribe(members => {
      this.members = members;
    });
  }

  addFolder(library: string) {

    const modalRef = this.modalService.open(DirectoryPickerComponent);
    //modalRef.componentInstance.name = 'World';
    modalRef.closed.subscribe((closeResult: DirectoryPickerResult) => {
      console.log('Closed Result', closeResult);
      if (closeResult.success) {
        console.log('Add folder path to Library');
      }
    });

  }
}
