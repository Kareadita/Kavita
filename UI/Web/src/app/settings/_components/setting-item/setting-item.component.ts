import {
  ChangeDetectionStrategy,
  Component,
  computed,
  contentChild,
  DestroyRef,
  effect,
  ElementRef,
  HostListener,
  inject,
  input,
  model,
  OnInit,
  Renderer2,
  signal,
  TemplateRef,
  viewChild
} from '@angular/core';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {TranslocoDirective} from "@jsverse/transloco";
import {NgTemplateOutlet} from "@angular/common";
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";
import {filter, fromEvent, tap} from "rxjs";
import {AbstractControl} from "@angular/forms";
import {ValidationErrorsComponent} from "../../../shared/_components/validation-errors/validation-errors.component";
import {generateUniqueId} from "../../../_helpers/random";
import {wireSettingControl} from "../../../_helpers/setting-item";


@Component({
  selector: 'app-setting-item',
  imports: [
    TranslocoDirective,
    NgTemplateOutlet,
    SafeHtmlPipe,
    ValidationErrorsComponent
  ],
  templateUrl: './setting-item.component.html',
  styleUrl: './setting-item.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SettingItemComponent implements OnInit {

  private readonly elementRef = inject(ElementRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly renderer = inject(Renderer2);

  title = input.required<string>();
  editLabel = input<string | undefined>();
  canEdit = input(true);
  showEdit = input(true);
  isEditMode = model(false);
  subtitle = input<string | undefined>();
  /* Required for accessibility, esp validations */
  labelId = input<string | undefined>();
  toggleOnViewClick = input(true);
  /**
   * When true, the hover animation will not be present and the titleExtras will be always visible
   */
  fixedExtras = input(false);
  control = input<AbstractControl | null>(null);
  /** Custom validation messages */
  validations = input<Record<string, string>>({});

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

  private readonly editWrapper = viewChild<ElementRef<HTMLElement>>('editWrapper');
  private readonly viewWrapper = viewChild<ElementRef<HTMLElement>>('viewWrapper');

  /** Where we search for inputs from **/
  private readonly wrapperScope = computed(() =>
    this.editWrapper()?.nativeElement ?? this.viewWrapper()?.nativeElement ?? null
  );
  private readonly generatedId = signal<string>(generateUniqueId());
  /** A unique id to wire id up */
  readonly elementId = computed(() => {
    return this.labelId() || this.generatedId();
  });

  protected readonly hasControl = wireSettingControl({
    scope: this.wrapperScope,
    elementId: this.elementId,
    describeValidation: computed(() => this.control() !== null),
    label: this.title,
  }).hasControl;




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
            if (!this.showEdit()) return;
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

  ngOnInit() {
    const control = this.control();
    if (!control) return;

    if (control.invalid && this.showEdit() && this.valueEditRef() != null) {
      this.isEditMode.set(true);
    }

    control.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef),
      filter(() => control.invalid && this.showEdit() && this.valueEditRef() != null),
      tap(() => this.isEditMode.set(true)),
    ).subscribe();
  }

  toggleEditMode() {
    if (!this.toggleOnViewClick()) return;
    if (!this.canEdit()) return;
    if (this.control() != null && this.control()!.invalid) return;

    this.isEditMode.set(!this.isEditMode());
    this.focusInput();
  }

  focusInput() {
    if (!this.isEditMode()) return;

    setTimeout(() => this.findFirstControl(this.wrapperScope())?.focus(), 10);
  }

  private findFirstControl(scope: HTMLElement | null): HTMLElement | null {
    if (!scope) return null;

    return scope.querySelector<HTMLElement>('input:not([type=hidden]), select, textarea');
  }

}
