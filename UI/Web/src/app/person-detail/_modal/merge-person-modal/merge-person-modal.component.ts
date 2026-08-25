import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  EventEmitter,
  inject,
  input,
  OnInit,
  signal
} from '@angular/core';
import {Person} from "../../../_models/metadata/person";
import {PersonService} from "../../../_services/person.service";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {TypeaheadComponent} from "../../../typeahead/_components/typeahead.component";
import {TypeaheadSettings} from "../../../typeahead/_models/typeahead-settings";
import {map} from "rxjs/operators";
import {UtilityService} from "../../../shared/_services/utility.service";
import {SettingItemComponent} from "../../../settings/_components/setting-item/setting-item.component";
import {BadgeExpanderComponent} from "../../../shared/badge-expander/badge-expander.component";
import {SeriesFilterField} from "../../../_models/metadata/v2/series-filter-field";
import {Observable, of} from "rxjs";
import {Series} from "../../../_models/series";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {AsyncPipe} from "@angular/common";
import {modalSaved} from "../../../_models/modal/modal-result";
import {TypeaheadSettingsFactoryService} from "../../../typeahead-settings-factory.service";

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
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './merge-person-modal.component.scss'
})
export class MergePersonModalComponent implements OnInit {

  private readonly personService = inject(PersonService);
  private readonly utilityService = inject(UtilityService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly modal = inject(NgbActiveModal);
  private readonly typeaheadSettingsFactory = inject(TypeaheadSettingsFactoryService);

  typeAheadSettings = signal<TypeaheadSettings<Person> | null>(null);
  typeAheadUnfocus = new EventEmitter<string>();

  person = input.required<Person>();

  mergee = signal<Person | null>(null);
  knownFor$: Observable<Series[]> | null = null;

  readonly allNewAliases = computed(() => {
    const mergee = this.mergee();
    if (!mergee) return [];

    return [mergee.name, ...mergee.aliases];
  });


  save() {
    const mergee = this.mergee();
    if (!mergee) {
      this.close();
      return;
    }

    this.personService.mergePerson(this.person().id, mergee.id).subscribe(person => {
      this.modal.close(modalSaved(person));
    })
  }

  close() {
    this.modal.dismiss();
  }

  ngOnInit(): void {

    this.typeAheadSettings.set(this.typeaheadSettingsFactory.forPerson({id: 'merge-person-modal-typeahead', addIfNonExisting: false,
      overrides: {
        fetchFn: (filter: string) => {
          if (filter.length == 0) return of([]);

          return this.personService.searchPerson(filter).pipe(map(people => {
            return people.filter(p => this.utilityService.filter(p.name, filter) && p.id != this.person().id);
          }));
        },
        multiple: false
      }}));
  }

  updatePerson(people: Person[]) {
    if (people.length == 0) return;

    this.typeAheadUnfocus.emit(this.typeAheadSettings()!.id);
    this.mergee.set(people[0]);

    this.knownFor$ = this.personService.getSeriesMostKnownFor(this.mergee()!.id)
        .pipe(takeUntilDestroyed(this.destroyRef));
  }

  protected readonly FilterField = SeriesFilterField;
}
