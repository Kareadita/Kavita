import { Component, OnInit } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { NgbActiveModal, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { LibraryService } from 'src/app/_services/library.service';
import { DirectoryPickerComponent, DirectoryPickerResult } from '../directory-picker/directory-picker.component';

@Component({
  selector: 'app-library-editor-modal',
  templateUrl: './library-editor-modal.component.html',
  styleUrls: ['./library-editor-modal.component.scss']
})
export class LibraryEditorModalComponent implements OnInit {


  libraryForm: FormGroup = new FormGroup({
    name: new FormControl('', [Validators.required]),
    type: new FormControl(0, [Validators.required])
  });

  selectedFolders: string[] = [];

  constructor(private modalService: NgbModal, private libraryService: LibraryService, public modal: NgbActiveModal) { }

  ngOnInit(): void {
  }

  submitLibrary() {
    const model = this.libraryForm.value;
    model.folders = this.selectedFolders;
    console.log('Creating library with: ', model);
    // this.libraryService.create(model).subscribe(() => {
    //   this.close(true);
    // });
  }

  close(returnVal= false) {
    this.modal.close(returnVal);
  }

  reset() {

  }

  openDirectoryPicker() {
    const modalRef = this.modalService.open(DirectoryPickerComponent);
    modalRef.closed.subscribe((closeResult: DirectoryPickerResult) => {
      if (closeResult.success) {
        this.selectedFolders.push(closeResult.folderPath);
      }
    });
  }



}
