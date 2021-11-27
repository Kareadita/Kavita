import { Component, Input, OnInit } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { SelectionModel } from 'src/app/typeahead/typeahead.component';
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
  selections!: SelectionModel<Library>;
  selectAll: boolean = false;
  isLoading: boolean = false;

  get hasSomeSelected() {
    console.log(this.selections != null && this.selections.hasSomeSelected());
    return this.selections != null && this.selections.hasSomeSelected();
  }

  constructor(public modal: NgbActiveModal, private libraryService: LibraryService, private fb: FormBuilder) { }

  ngOnInit(): void {
    this.libraryService.getLibraries().subscribe(libs => {
      this.allLibraries = libs;
      this.setupSelections();
    });
  }

  close() {
    this.modal.dismiss();
  }

  save() {
    if (this.member?.username === undefined) {
      return;
    }

    const selectedLibraries = this.selections.selected();
    this.libraryService.updateLibrariesForMember(this.member?.username, selectedLibraries).subscribe(() => {
      this.modal.close(true);
    });
  }

  setupSelections() {
    this.selections = new SelectionModel<Library>(false, this.allLibraries);
    this.isLoading = false;
      
    // If a member is passed in, then auto-select their libraries
    if (this.member !== undefined) {
      this.member.libraries.forEach(lib => {
        this.selections.toggle(lib, true, (a, b) => a.name === b.name);
      });
      this.selectAll = this.selections.selected().length === this.allLibraries.length;
    }
  }

  reset() {
    this.setupSelections();
  }

  toggleAll() {
    this.selectAll = !this.selectAll;
    this.allLibraries.forEach(s => this.selections.toggle(s, this.selectAll));
  }

  handleSelection(item: Library) {
    this.selections.toggle(item);
    const numberOfSelected = this.selections.selected().length;
    if (numberOfSelected == 0) {
      this.selectAll = false;
    } else if (numberOfSelected == this.selectedLibraries.length) {
      this.selectAll = true;
    }
  }

}
