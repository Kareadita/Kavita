import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  EventEmitter,
  inject,
  Input,
  OnInit,
  Output
} from '@angular/core';
import {FormsModule, ReactiveFormsModule} from '@angular/forms';
import {Library} from 'src/app/_models/library/library';
import {Member} from 'src/app/_models/auth/member';
import {LibraryService} from 'src/app/_services/library.service';
import {TranslocoDirective} from "@jsverse/transloco";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {SelectionModel} from "../../typeahead/_models/selection-model";

@Component({
    selector: 'app-library-selector',
    templateUrl: './library-selector.component.html',
    styleUrls: ['./library-selector.component.scss'],
    standalone: true,
  imports: [ReactiveFormsModule, FormsModule, TranslocoDirective, LoadingComponent],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LibrarySelectorComponent implements OnInit {

  private readonly libraryService = inject(LibraryService);
  private readonly cdRef = inject(ChangeDetectorRef);

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
    this.cdRef.markForCheck();
  }

  toggleAll() {
    this.selectAll = !this.selectAll;
    this.allLibraries.forEach(s => this.selections.toggle(s, this.selectAll));
    this.selected.emit(this.selections.selected());
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
    this.selected.emit(this.selections.selected());
  }

}
