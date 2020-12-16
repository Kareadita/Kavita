import { Component, OnInit } from '@angular/core';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { DirectoryPickerComponent, DirectoryPickerResult } from 'src/app/directory-picker/directory-picker.component';

@Component({
  selector: 'app-manage-library',
  templateUrl: './manage-library.component.html',
  styleUrls: ['./manage-library.component.scss']
})
export class ManageLibraryComponent implements OnInit {

  constructor(private modalService: NgbModal) { }

  ngOnInit(): void {
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
