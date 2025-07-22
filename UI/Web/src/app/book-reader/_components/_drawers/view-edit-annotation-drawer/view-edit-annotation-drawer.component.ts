import {ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, model, Signal} from '@angular/core';
import {NgbActiveOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {AnnotationService} from "../../../../_services/annotation.service";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {Annotation} from "../../../_models/annotations/annotation";
import {QuillEditorComponent, QuillViewComponent} from "ngx-quill";
import {TranslocoDirective} from "@jsverse/transloco";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {debounceTime, switchMap} from "rxjs/operators";
import {of} from "rxjs";
import {HighlightBarComponent} from "../../_annotations/highlight-bar/highlight-bar.component";
import {SlotColorPipe} from "../../../../_pipes/slot-color.pipe";
import {User} from "../../../../_models/user";

@Component({
    selector: 'app-view-edit-annotation-drawer',
  imports: [
    QuillEditorComponent,
    ReactiveFormsModule,
    QuillViewComponent,
    TranslocoDirective,
    HighlightBarComponent
  ],
    templateUrl: './view-edit-annotation-drawer.component.html',
    styleUrl: './view-edit-annotation-drawer.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
  })
  export class ViewEditAnnotationDrawerComponent {
    private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
    private readonly annotationService = inject(AnnotationService);
    private readonly destroyRef = inject(DestroyRef);
    private readonly highlightSlotPipe = new SlotColorPipe();

    annotation = model<Annotation | null>(null);
    isEditMode = model(false);
    user = model<User | null>(null);
    titleClass: Signal<string>;


    formGroup!: FormGroup;
    annotationNote: object = {};
    isSetup = false;

    constructor() {
      this.titleClass = computed(() => {
        const annotation = this.annotation();
        const slots = this.annotationService.slots() || [];

        if (!annotation || slots.length === 0) return '';
        const selectedSlot = slots[annotation.selectedSlotIndex];

        return `${this.highlightSlotPipe.transform(selectedSlot.color)}-title`;
      });


      effect(() => {
        const isEditMode = this.isEditMode();
        const annotation = this.annotation();

        // Side effect - patch in the current note
        // Parse the stored JSON string back to Delta object
        this.annotationNote = annotation?.comment ? JSON.parse(annotation.comment) : {}
        this.formGroup =  new FormGroup({
          'note': new FormControl(this.annotationNote, []),
          'hasSpoiler': new FormControl(annotation?.containsSpoiler, []),
          'selectedSlotIndex': new FormControl(annotation?.selectedSlotIndex ?? 0, [])
        });

        if (isEditMode && !this.isSetup) {
          this.formGroup.valueChanges.pipe(
            debounceTime(350),
            switchMap(_ => {
              if (!annotation) return of();

              annotation.containsSpoiler = this.formGroup.get('hasSpoiler')!.value;
              annotation.comment = JSON.stringify(this.annotationNote);
              console.log('saving annotation', annotation);

              return this.annotationService.updateAnnotation(annotation);
            }),
            takeUntilDestroyed(this.destroyRef)
          ).subscribe();

          this.isSetup = true;
        }
      });
    }

    updateSlotColor() {
      // TODO: This will emit slotUpdate and update the user preferences

    }



    changeSlotIndex(slotIndex: number) {
      const annotation = this.annotation();

      if (annotation) {
        this.annotation.set({...annotation, selectedSlotIndex: slotIndex});
        this.formGroup.get('selectedSlotIndex')?.setValue(slotIndex);
      }
    }


    close() {
      this.activeOffcanvas.close();
    }

    updateContent(event: any) {
      this.annotationNote = event.content;
    }
  }
