import { Component, Input, OnInit } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { Library } from 'src/app/_models/library';
import { Member } from 'src/app/_models/member';
import { LibraryService } from 'src/app/_services/library.service';

@Component({
  selector: 'app-library-access-modal',
  templateUrl: './library-access-modal.component.html',
  styleUrls: ['./library-access-modal.component.scss']
})
export class LibraryAccessModalComponent implements OnInit {

  @Input() member: Member | undefined;
  libraries: Library[] = [];

  constructor(public modal: NgbActiveModal, private libraryService: LibraryService, private fb: FormBuilder) { }

  ngOnInit(): void {
    this.libraryService.getLibrariesForMember(this.member?.username + '').subscribe(libs => {
      this.libraries = libs;
    });
  }

  close() {
    this.modal.close();
  }

  save() {

  }

  reset() {

  }

}
