import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ContentChild, ElementRef, EventEmitter, HostListener,
  inject,
  Input, Output,
  TemplateRef
} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {NgTemplateOutlet} from "@angular/common";
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";
import {filter, fromEvent, tap} from "rxjs";
import {AbstractControl, FormControl} from "@angular/forms";

@Component({
  selector: 'app-setting-item',
  standalone: true,
  imports: [
    TranslocoDirective,
    NgTemplateOutlet,
    SafeHtmlPipe
  ],
  templateUrl: './setting-item.component.html',
  styleUrl: './setting-item.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SettingItemComponent {

  private readonly cdRef = inject(ChangeDetectorRef);

  @Input({required:true}) title: string = '';
  @Input() editLabel: string | undefined = undefined;
  @Input() canEdit: boolean = true;
  @Input() showEdit: boolean = true;
  @Input() isEditMode: boolean = false;
  @Input() subtitle: string | undefined = undefined;
  @Input() labelId: string | undefined = undefined;
  @Input() toggleOnViewClick: boolean = true;
  @Input() control: AbstractControl<any> | null = null;
  @Output() editMode = new EventEmitter<boolean>();

  /**
   * Extra information to show next to the title
   */
  @ContentChild('titleExtra') titleExtraRef!: TemplateRef<any>;
  /**
   * View in View mode
   */
  @ContentChild('view') valueViewRef!: TemplateRef<any>;
  /**
   * View in Edit mode
   */
  @ContentChild('edit') valueEditRef!: TemplateRef<any>;
  /**
   * Extra button controls to show instead of Edit
   */
  @ContentChild('titleActions') titleActionsRef!: TemplateRef<any>;

  @HostListener('click', ['$event'])
  onClickInside(event: MouseEvent) {
    event.stopPropagation(); // Prevent the click from bubbling up
  }

  constructor(elementRef: ElementRef) {
    if (!this.toggleOnViewClick) return;

    fromEvent(window, 'click')
      .pipe(
        filter((event: Event) => {
          if (!this.toggleOnViewClick) return false;
          if (this.control != null && this.control.invalid) return false;

          const mouseEvent = event as MouseEvent;
          const selection = window.getSelection();
          const hasSelection = selection !== null && selection.toString().trim() === '';
          return !elementRef.nativeElement.contains(mouseEvent.target) && hasSelection;
        }),
        tap(() => {
          this.isEditMode = false;
          this.editMode.emit(this.isEditMode);
          this.cdRef.markForCheck();
        })
      )
      .subscribe();
  }

  toggleEditMode() {

    if (!this.toggleOnViewClick) return;
    if (!this.canEdit) return;
    if (this.control != null && this.control.invalid) return;

    this.isEditMode = !this.isEditMode;
    this.editMode.emit(this.isEditMode);
    this.cdRef.markForCheck();
  }

}
