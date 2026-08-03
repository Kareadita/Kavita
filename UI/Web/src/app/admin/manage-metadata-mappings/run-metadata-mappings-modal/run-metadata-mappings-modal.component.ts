import {ChangeDetectionStrategy, Component, inject, OnInit, signal} from '@angular/core';
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {SettingsService} from "../../settings.service";
import {LibraryService} from "../../../_services/library.service";
import {
  AbstractControl,
  FormArray,
  FormControl,
  FormGroup,
  FormsModule,
  NonNullableFormBuilder,
  ReactiveFormsModule, ValidationErrors, ValidatorFn
} from "@angular/forms";
import {Library} from "../../../_models/library/library";
import {catchError, finalize, map, tap} from "rxjs/operators";
import {TypeaheadSettings} from "../../../typeahead/_models/typeahead-settings";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {TypeaheadComponent} from "../../../typeahead/_components/typeahead.component";
import {of} from "rxjs";
import {SettingItemComponent} from "../../../settings/_components/setting-item/setting-item.component";
import {ToastrService} from "ngx-toastr";

type RunMetadataMappingsRequestFormGroup = FormGroup<{
  allLibraries: FormControl<boolean>,
  includedLibraries: FormControl<number[]>,
  excludedLibraries: FormControl<number[]>,
}>;

@Component({
  selector: 'app-run-metadata-mappings-modal',
  imports: [
    FormsModule,
    ReactiveFormsModule,
    TranslocoDirective,
    TypeaheadComponent,
    SettingItemComponent
  ],
  templateUrl: './run-metadata-mappings-modal.component.html',
  styleUrl: './run-metadata-mappings-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RunMetadataMappingsModalComponent implements OnInit {

  private readonly modal = inject(NgbActiveModal);
  private readonly settingsService = inject(SettingsService);
  private readonly libraryService = inject(LibraryService);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly toastR = inject(ToastrService);

  libraries = signal<Library[]>([]);
  requestForm!: RunMetadataMappingsRequestFormGroup;
  isSaving = signal(false);

  includedLibrariesTypeaheadSettings = signal<TypeaheadSettings<Library> | null>(null);
  excludedLibrariesTypeaheadSettings = signal<TypeaheadSettings<Library> | null>(null);

  ngOnInit() {
    this.requestForm = this.fb.group({
      allLibraries: this.fb.control(false),
      includedLibraries: this.fb.control<number[]>([]),
      excludedLibraries: this.fb.control<number[]>([]),
    }, { validators: [atLeastOneLibraryValidator()] });

    this.libraryService.getLibraries().pipe(
      tap(libraries => this.libraries.set(libraries)),
      tap(() => this.setupTypeaheads()),
    ).subscribe();
  }

  private setupTypeaheads() {
    const includedSettings = new TypeaheadSettings<Library>();
    includedSettings.id = 'included-libraries';
    includedSettings.fetchFn = (query) => {
      const excludedLibraries = this.requestForm.get('excludedLibraries')!.value;

      return of(this.libraries()
        .filter(l => !excludedLibraries.includes(l.id))
        .filter(l => l.name.toLowerCase().includes(query.toLowerCase())));
    };
    includedSettings.compareFn = (libraries, query) => libraries.filter(l => l.name.toLowerCase().includes(query.toLowerCase()));
    includedSettings.multiple = true;
    includedSettings.trackByIdentityFn = (index, lib) => lib.id + '';
    includedSettings.unique = true;
    includedSettings.minCharacters = 0;
    includedSettings.dropdownPosition = 'body';

    const excludedSettings = new TypeaheadSettings<Library>();
    excludedSettings.id = 'excluded-libraries';
    excludedSettings.fetchFn = (query) => {
      const includedLibraries = this.requestForm.get('includedLibraries')!.value;

      return of(this.libraries()
        .filter(l => !includedLibraries.includes(l.id))
        .filter(l => l.name.toLowerCase().includes(query.toLowerCase())));
    };
    excludedSettings.compareFn = (libraries, query) => libraries.filter(l => l.name.toLowerCase().includes(query.toLowerCase()));
    excludedSettings.multiple = true;
    excludedSettings.trackByIdentityFn = (index, lib) => lib.id + '';
    excludedSettings.unique = true;
    excludedSettings.minCharacters = 0;
    excludedSettings.dropdownPosition = 'body';

    this.includedLibrariesTypeaheadSettings.set(includedSettings);
    this.excludedLibrariesTypeaheadSettings.set(excludedSettings);
  }

  protected updateLibrarySelection(formControl: 'includedLibraries' | 'excludedLibraries', libraries: Library[]) {
    this.requestForm.get(formControl)?.setValue(libraries.map(l => l.id));
  }

  protected close() {
    this.modal.close();
  }

  protected submit() {
    const request = this.requestForm.getRawValue();

    this.settingsService.runMetadataMappings(request).pipe(
      tap(() => this.toastR.info(
        translate('run-metadata-mappings-modal.queued-description'),
        translate('run-metadata-mappings-modal.queued-title')
      )),
      finalize(() => this.close())
    ).subscribe();
  }

}

function atLeastOneLibraryValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const allLibraries = control.get('allLibraries')?.value;
    const includedLibraries = control.get('includedLibraries')?.value;

    const hasAllLibraries = !!allLibraries;
    const hasIncludedLibraries = Array.isArray(includedLibraries) && includedLibraries.length > 0;

    if (hasAllLibraries || hasIncludedLibraries) {
      return null;
    }

    return { librarySelectionRequired: true };
  };
}
