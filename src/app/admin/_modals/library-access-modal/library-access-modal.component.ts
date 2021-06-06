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
  allLibraries: Library[] = [];
  selectedLibraries: Array<{selected: boolean, data: Library}> = [];

  constructor(public modal: NgbActiveModal, private libraryService: LibraryService, private fb: FormBuilder) { }

  ngOnInit(): void {
    this.libraryService.getLibraries().subscribe(libs => {
      this.allLibraries = libs;
      this.selectedLibraries = libs.map(item => {
        return {selected: false, data: item};
      });

      if (this.member !== undefined) {
        this.member.libraries.forEach(lib => {
          const foundLibrary = this.selectedLibraries.filter(item => item.data.name === lib.name);
          if (foundLibrary.length > 0) {
            foundLibrary[0].selected = true;
          }
        });
      }
    });
  }

  close() {
    this.modal.close(false);
  }

  save() {
    if (this.member?.username === undefined) {
      return;
    }

    const selectedLibraries = this.selectedLibraries.filter(item => item.selected).map(item => item.data);
    this.libraryService.updateLibrariesForMember(this.member?.username, selectedLibraries).subscribe(() => {
      this.modal.close(true);
    });
  }

  reset() {
    this.selectedLibraries = this.allLibraries.map(item => {
      return {selected: false, data: item};
    });


    if (this.member !== undefined) {
      this.member.libraries.forEach(lib => {
        const foundLibrary = this.selectedLibraries.filter(item => item.data.name === lib.name);
        if (foundLibrary.length > 0) {
          foundLibrary[0].selected = true;
        }
      });
    }
  }

}
