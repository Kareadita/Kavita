import {
  ChangeDetectionStrategy,
  Component,
  contentChild,
  DestroyRef,
  effect,
  ElementRef,
  HostListener,
  inject,
  input,
  model,
  TemplateRef
} from '@angular/core';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {TranslocoDirective} from "@jsverse/transloco";
import {NgTemplateOutlet} from "@angular/common";
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";
import {filter, fromEvent, tap} from "rxjs";
import {AbstractControl} from "@angular/forms";

@Component({
  selector: 'app-setting-item',
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

  private readonly elementRef = inject(ElementRef);
  private readonly destroyRef = inject(DestroyRef);

  title = input.required<string>();
  editLabel = input<string | undefined>();
  canEdit = input(true);
  showEdit = input(true);
  isEditMode = model(false);
  subtitle = input<string | undefined>();
  labelId = input<string | undefined>();
  toggleOnViewClick = input(true);
  /**
   * When true, the hover animation will not be present and the titleExtras will be always visible
   */
  fixedExtras = input(false);
  control = input<AbstractControl<any> | null>(null);

  /**
   * When true, allows click events to bubble up to components within
   */
  allowClickEvents = input(false);

  /**
   * Extra information to show next to the title
   */
  titleExtraRef = contentChild<TemplateRef<any>>('titleExtra');
  /**
   * View in View mode
   */
  valueViewRef = contentChild<TemplateRef<any>>('view');
  /**
   * View in Edit mode
   */
  valueEditRef = contentChild<TemplateRef<any>>('edit');
  /**
   * Extra button controls to show instead of Edit
   */
  titleActionsRef = contentChild<TemplateRef<any>>('titleActions');

  @HostListener('click', ['$event'])
  onClickInside(event: MouseEvent) {
    if (this.allowClickEvents()) return;

    event.stopPropagation(); // Prevent the click from bubbling up
  }

  constructor() {
    if (this.toggleOnViewClick()) {
      fromEvent(window, 'click')
        .pipe(
          takeUntilDestroyed(this.destroyRef),
          filter((event: Event) => {
            if (!this.toggleOnViewClick()) return false;
            if (this.control() != null && this.control()!.invalid) return false;

            const mouseEvent = event as MouseEvent;
            const selection = window.getSelection();
            const hasSelection = selection !== null && selection.toString().trim() === '';
            return !this.elementRef.nativeElement.contains(mouseEvent.target) && hasSelection;
          }),
          tap(() => {
            this.isEditMode.set(false);
          })
        )
        .subscribe();
    }

    let initialized = false;
    effect(() => {
      const editMode = this.isEditMode();
      if (!initialized) { initialized = true; return; }
      if (!this.toggleOnViewClick()) return;
      if (!this.canEdit()) return;
      if (this.control() != null && this.control()!.invalid) return;
      if (editMode) this.focusInput();
    });
  }

  toggleEditMode() {
    if (!this.toggleOnViewClick()) return;
    if (!this.canEdit()) return;
    if (this.control() != null && this.control()!.invalid) return;

    this.isEditMode.set(!this.isEditMode());
    this.focusInput();
  }

  focusInput() {
    if (this.isEditMode()) {
      setTimeout(() => {
        const inputElem = this.findFirstInput();
        if (inputElem) {
          inputElem.focus();
        }
      }, 10);
    }
  }

  private findFirstInput(): HTMLInputElement | null {
    const nativeInputs = [...this.elementRef.nativeElement.querySelectorAll('input'), ...this.elementRef.nativeElement.querySelectorAll('select'), ...this.elementRef.nativeElement.querySelectorAll('textarea')];
    if (nativeInputs.length === 0) return null;

    return nativeInputs[0];
  }
}
