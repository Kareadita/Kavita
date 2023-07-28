import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { Library } from 'src/app/_models/library';
import { Member } from 'src/app/_models/auth/member';
import { LibraryService } from 'src/app/_services/library.service';
import { SelectionModel } from 'src/app/typeahead/_components/typeahead.component';
import { NgIf, NgFor } from '@angular/common';
import {TranslocoModule} from "@ngneat/transloco";

@Component({
    selector: 'app-library-selector',
    templateUrl: './library-selector.component.html',
    styleUrls: ['./library-selector.component.scss'],
    standalone: true,
  imports: [NgIf, ReactiveFormsModule, FormsModule, NgFor, TranslocoModule]
})
export class LibrarySelectorComponent implements OnInit {

  @Input() member: Member | undefined;
  @Output() selected: EventEmitter<Array<Library>> = new EventEmitter<Array<Library>>();

  allLibraries: Library[] = [];
  selectedLibraries: Array<{selected: boolean, data: Library}> = [];
  selections!: SelectionModel<Library>;
  selectAll: boolean = false;
  isLoading: boolean = false;

  get hasSomeSelected() {
    return this.selections != null && this.selections.hasSomeSelected();
  }

  constructor(private libraryService: LibraryService, private fb: FormBuilder) { }

  ngOnInit(): void {
    this.libraryService.getLibraries().subscribe(libs => {
      this.allLibraries = libs;
      this.setupSelections();
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
      this.selected.emit(this.selections.selected());
    }
  }

  toggleAll() {
    this.selectAll = !this.selectAll;
    this.allLibraries.forEach(s => this.selections.toggle(s, this.selectAll));
    this.selected.emit(this.selections.selected());
  }

  handleSelection(item: Library) {
    this.selections.toggle(item);
    const numberOfSelected = this.selections.selected().length;
    if (numberOfSelected == 0) {
      this.selectAll = false;
    } else if (numberOfSelected == this.selectedLibraries.length) {
      this.selectAll = true;
    }

    this.selected.emit(this.selections.selected());
  }

}
