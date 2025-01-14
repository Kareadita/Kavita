import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
import {Chapter} from "../../_models/chapter";
import {AsyncPipe, DatePipe, NgForOf, TitleCasePipe} from "@angular/common";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {FullProgress} from "../../_models/readers/full-progress";
import {ReaderService} from "../../_services/reader.service";
import {TranslocoDirective} from "@jsverse/transloco";
import {FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {SentenceCasePipe} from "../../_pipes/sentence-case.pipe";
import {DefaultDatePipe} from "../../_pipes/default-date.pipe";
import {ColumnMode, NgxDatatableModule} from "@siemens/ngx-datatable";

@Component({
  selector: 'app-edit-chapter-progress',
  standalone: true,
  imports: [
    AsyncPipe,
    DefaultValuePipe,
    NgForOf,
    TitleCasePipe,
    UtcToLocalTimePipe,
    TranslocoDirective,
    ReactiveFormsModule,
    SentenceCasePipe,
    DatePipe,
    DefaultDatePipe,
    NgxDatatableModule
  ],
  templateUrl: './edit-chapter-progress.component.html',
  styleUrl: './edit-chapter-progress.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditChapterProgressComponent implements OnInit {

  private readonly readerService = inject(ReaderService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly fb = inject(FormBuilder);

  @Input({required: true}) chapter!: Chapter;

  progressEvents: Array<FullProgress> = [];
  editMode: {[key: number]: boolean} = {};
  formGroup = this.fb.group({
    items: this.fb.array([])
  });

  get items() {
    return this.formGroup.get('items') as FormArray;
  }


  ngOnInit() {
    this.readerService.getAllProgressForChapter(this.chapter!.id).subscribe(res => {
      this.progressEvents = res;
      this.progressEvents.forEach((v, i) => {
        this.editMode[i] = false;
        this.items.push(this.createRowForm(v));
      });
      this.cdRef.markForCheck();
    });
  }

  createRowForm(progress: FullProgress): FormGroup {
    return this.fb.group({
      pagesRead: [progress.pagesRead, [Validators.required, Validators.min(0), Validators.max(this.chapter!.pages)]],
      created: [progress.createdUtc, [Validators.required]],
      lastModified: [progress.lastModifiedUtc, [Validators.required]],
    });
  }

  edit(progress: FullProgress, idx: number) {
    this.editMode[idx] = !this.editMode[idx];
    this.cdRef.markForCheck();
  }

  save(progress: FullProgress, idx: number) {
    // todo
    this.editMode[idx] = !this.editMode[idx];
    // this.formGroup[idx + ''].patchValue({
    //   pagesRead: progress.pagesRead,
    //   // Patch other form values as needed
    // });
    this.cdRef.markForCheck();
  }

  protected readonly ColumnMode = ColumnMode;
}
