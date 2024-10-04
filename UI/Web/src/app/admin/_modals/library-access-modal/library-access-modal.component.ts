import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
import {NgbActiveModal} from '@ng-bootstrap/ng-bootstrap';
import {Library} from 'src/app/_models/library/library';
import {Member} from 'src/app/_models/auth/member';
import {LibraryService} from 'src/app/_services/library.service';
import {NgFor, NgIf} from '@angular/common';
import {FormsModule, ReactiveFormsModule} from '@angular/forms';
import {TranslocoDirective} from "@jsverse/transloco";
import {SelectionModel} from "../../../typeahead/_models/selection-model";

@Component({
  selector: 'app-library-access-modal',
  templateUrl: './library-access-modal.component.html',
  styleUrls: ['./library-access-modal.component.scss'],
  standalone: true,
  imports: [ReactiveFormsModule, FormsModule, NgFor, NgIf, TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LibraryAccessModalComponent implements OnInit {

  protected readonly modal = inject(NgbActiveModal);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly libraryService = inject(LibraryService);

  @Input() member: Member | undefined;
  allLibraries: Library[] = [];
  selectedLibraries: Array<{selected: boolean, data: Library}> = [];
  selections!: SelectionModel<Library>;
  selectAll: boolean = false;

  get hasSomeSelected() {
    return this.selections != null && this.selections.hasSomeSelected();
  }


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

    // If a member is passed in, then auto-select their libraries
    if (this.member !== undefined) {
      this.member.libraries.forEach(lib => {
        this.selections.toggle(lib, true, (a, b) => a.name === b.name);
      });
      this.selectAll = this.selections.selected().length === this.allLibraries.length;
    }
    this.cdRef.markForCheck();
  }

  reset() {
    this.setupSelections();
  }

  toggleAll() {
    this.selectAll = !this.selectAll;
    this.allLibraries.forEach(s => this.selections.toggle(s, this.selectAll));
    this.cdRef.markForCheck();
  }

  handleSelection(item: Library) {
    this.selections.toggle(item);
    const numberOfSelected = this.selections.selected().length;
    if (numberOfSelected == 0) {
      this.selectAll = false;
    } else if (numberOfSelected == this.selectedLibraries.length) {
      this.selectAll = true;
    }
    this.cdRef.markForCheck();
  }

}
