import { Component, Input, OnChanges, OnInit } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { NgbActiveModal, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { Library } from 'src/app/_models/library';
import { LibraryService } from 'src/app/_services/library.service';
import { DirectoryPickerComponent, DirectoryPickerResult } from '../directory-picker/directory-picker.component';

@Component({
  selector: 'app-library-editor-modal',
  templateUrl: './library-editor-modal.component.html',
  styleUrls: ['./library-editor-modal.component.scss']
})
export class LibraryEditorModalComponent implements OnInit, OnChanges {

  @Input() library: Library | undefined = undefined;

  libraryForm: FormGroup = new FormGroup({
    name: new FormControl('', [Validators.required]),
    type: new FormControl(0, [Validators.required])
  });

  selectedFolders: string[] = [];

  constructor(private modalService: NgbModal, private libraryService: LibraryService, public modal: NgbActiveModal) { }

  ngOnInit(): void {
    this.setValues();
  }

  ngOnChanges() {
    console.log('Library: ', this.library);
  }

  submitLibrary() {
    const model = this.libraryForm.value;
    model.folders = this.selectedFolders;

    if (this.library !== undefined) {
      model.id = this.library.id;
      this.libraryService.update(model).subscribe(() => {
        this.close(true);
      });
    } else {
      this.libraryService.create(model).subscribe(() => {
        this.close(true);
      });
    }
  }

  close(returnVal= false) {
    this.modal.close(returnVal);
  }

  reset() {
    this.setValues();
  }

  setValues() {
    if (this.library !== undefined) {
      this.libraryForm.get('name')?.setValue(this.library.name);
      this.libraryForm.get('type')?.setValue(this.library.type);
      this.selectedFolders = this.library.folders;
    }
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
