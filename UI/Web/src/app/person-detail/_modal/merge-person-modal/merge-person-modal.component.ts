import {Component, DestroyRef, EventEmitter, inject, Input, OnInit} from '@angular/core';
import {Person} from "../../../_models/metadata/person";
import {PersonService} from "../../../_services/person.service";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {ToastrService} from "ngx-toastr";
import {TranslocoDirective} from "@jsverse/transloco";
import {TypeaheadComponent} from "../../../typeahead/_components/typeahead.component";
import {TypeaheadSettings} from "../../../typeahead/_models/typeahead-settings";
import {map} from "rxjs/operators";
import {UtilityService} from "../../../shared/_services/utility.service";
import {SettingItemComponent} from "../../../settings/_components/setting-item/setting-item.component";
import {BadgeExpanderComponent} from "../../../shared/badge-expander/badge-expander.component";
import {FilterField} from "../../../_models/metadata/v2/filter-field";
import {Observable, of} from "rxjs";
import {Series} from "../../../_models/series";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {AsyncPipe} from "@angular/common";

@Component({
  selector: 'app-merge-person-modal',
  imports: [
    TranslocoDirective,
    TypeaheadComponent,
    SettingItemComponent,
    BadgeExpanderComponent,
    AsyncPipe
  ],
  templateUrl: './merge-person-modal.component.html',
  styleUrl: './merge-person-modal.component.scss'
})
export class MergePersonModalComponent implements OnInit {

  private readonly personService = inject(PersonService);
  public readonly utilityService = inject(UtilityService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly modal = inject(NgbActiveModal);
  protected readonly toastr = inject(ToastrService);

  typeAheadSettings!: TypeaheadSettings<Person>;
  typeAheadUnfocus = new EventEmitter<string>();

  @Input({required: true}) person!: Person;

  mergee: Person | null = null;
  knownFor$: Observable<Series[]> | null = null;

  save() {
    if (!this.mergee) {
      this.close();
      return;
    }

    this.personService.mergePerson(this.person.id, this.mergee.id).subscribe(person => {
      this.modal.close({success: true, person: person});
    })
  }

  close() {
    this.modal.close({success: false, person: this.person});
  }

  ngOnInit(): void {
    this.typeAheadSettings = new TypeaheadSettings<Person>();
    this.typeAheadSettings.minCharacters = 0;
    this.typeAheadSettings.multiple = false;
    this.typeAheadSettings.addIfNonExisting = false;
    this.typeAheadSettings.id = "merge-person-modal-typeahead";
    this.typeAheadSettings.compareFn = (options: Person[], filter: string) => {
      return options.filter(m => this.utilityService.filter(m.name, filter));
    }
    this.typeAheadSettings.selectionCompareFn = (a: Person, b: Person) => {
      return a.name == b.name;
    }
    this.typeAheadSettings.fetchFn = (filter: string) => {
      if (filter.length == 0) return of([]);

      return this.personService.searchPerson(filter).pipe(map(people => {
        return people.filter(p => this.utilityService.filter(p.name, filter) && p.id != this.person.id);
      }));
    };

    this.typeAheadSettings.trackByIdentityFn = (index, value) => `${value.name}_${value.id}`;
  }

  updatePerson(people: Person[]) {
    if (people.length == 0) return;

    this.typeAheadUnfocus.emit(this.typeAheadSettings.id);
    this.mergee = people[0];
    this.knownFor$ = this.personService.getSeriesMostKnownFor(this.mergee.id)
        .pipe(takeUntilDestroyed(this.destroyRef));
  }

  protected readonly FilterField = FilterField;

  allNewAliases() {
    if (!this.mergee) return [];

    return [this.mergee.name, ...this.mergee.aliases]
  }
}
