import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  model,
  signal,
  Signal
} from '@angular/core';
import {NgbActiveOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {AnnotationService} from "../../../../_services/annotation.service";
import {NgClass, NgStyle} from "@angular/common";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {allHighlightColors, Annotation, HighlightColor} from "../../../_models/annotation";
import {QuillEditorComponent, QuillViewComponent} from "ngx-quill";
import {HighlightColorPipe} from "../../../../_pipes/highlight-color.pipe";
import {TranslocoDirective} from "@jsverse/transloco";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {debounceTime, switchMap} from "rxjs/operators";
import {of, Subscription} from "rxjs";

@Component({
  selector: 'app-view-edit-annotation-drawer',
  imports: [
    QuillEditorComponent,
    NgStyle,
    ReactiveFormsModule,
    HighlightColorPipe,
    QuillViewComponent,
    TranslocoDirective,
    NgClass
  ],
  templateUrl: './view-edit-annotation-drawer.component.html',
  styleUrl: './view-edit-annotation-drawer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ViewEditAnnotationDrawerComponent {
  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  private readonly annotationService = inject(AnnotationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly highlightColorPipe = new HighlightColorPipe();

  annotation = model<Annotation | null>(null);
  isEditMode = model(false);
  titleClass: Signal<string>;

  formGroup!: FormGroup;
  annotationNote = '';
  /**
   * Save pipeline
   * @private
   */
  private readonly formSubscription = signal<Subscription | null>(null);

  constructor() {
    this.formGroup =  new FormGroup({
      'note': new FormControl(this.annotation()?.comment || '', []),
      'hasSpoiler': new FormControl(false, [])
    });

    this.titleClass = computed(() => {
      const annotation = this.annotation();
      if (!annotation) return '';

      return `${this.highlightColorPipe.transform(annotation.highlightColor)}-title`;
    });


    effect(() => {
      this.formGroup.get('note')!.patchValue(this.annotation()?.comment);
    });

    effect(() => {
      const isEditMode = this.isEditMode();
      const currentSubscription = this.formSubscription();

      if (isEditMode && !currentSubscription) {
        const subscription = this.formGroup.valueChanges.pipe(
          debounceTime(350),
          switchMap(_ => {
            const annotation = this.annotation();
            if (!annotation) return of();

            annotation.containsSpoiler = this.formGroup.get('hasSpoiler')!.value;
            annotation.comment = JSON.stringify(this.annotationNote);

            return this.annotationService.updateAnnotation(annotation);
          }),
          takeUntilDestroyed(this.destroyRef)
        ).subscribe();

        this.formSubscription.set(subscription);
      } else if (!isEditMode && currentSubscription) {
        currentSubscription.unsubscribe();
        this.formSubscription.set(null);
      }
    });
  }


  changeHighlight(highlight: HighlightColor) {
    let annotation = this.annotation();
    if (annotation) {
      this.annotation.set({...annotation, highlightColor: highlight});
    }
  }


  close() {
    this.activeOffcanvas.close();
  }

  updateContent(event: any) {
    this.annotationNote = event.content;
  }

  cancelEvent(event: any) {
    event.stopPropagation();
    event.preventDefault();
  }

  protected readonly HighlightColor = HighlightColor;
  protected readonly allHighlightColors = allHighlightColors;
}
