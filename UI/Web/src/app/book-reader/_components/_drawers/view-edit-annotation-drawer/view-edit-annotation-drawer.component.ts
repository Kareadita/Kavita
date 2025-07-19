import {Component, computed, effect, inject, model, Signal} from '@angular/core';
import {NgbActiveOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {AnnotationService} from "../../../../_services/annotation.service";
import {NgClass, NgStyle} from "@angular/common";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {allHighlightColors, Annotation, HighlightColor} from "../../../_models/annotation";
import {QuillEditorComponent, QuillViewComponent} from "ngx-quill";
import {HighlightColorPipe} from "../../../../_pipes/highlight-color.pipe";
import {TranslocoDirective} from "@jsverse/transloco";

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
  styleUrl: './view-edit-annotation-drawer.component.scss'
})
export class ViewEditAnnotationDrawerComponent {
  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  private readonly annotationService = inject(AnnotationService);
  private readonly highlightColorPipe = new HighlightColorPipe();

  annotation = model<Annotation | null>(null);
  isEditMode = model(false);
  titleClass: Signal<string>;

  formGroup!: FormGroup;
  annotationNote = '';

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
  }


  save() {
    const annotation = this.annotation();
    if (!annotation) return;

    annotation.containsSpoiler = this.formGroup.get('hasSpoiler')!.value;
    annotation.comment = JSON.stringify(this.annotationNote);

    // this.annotationService.updateAnnotation(annotation).subscribe(res => {
    //   this.close(); // TODO: Maybe pass the state back?
    // });
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
