import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  EventEmitter,
  inject,
  model,
  Output
} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {HighlightSlot} from "../../../_models/annotations/highlight-slot";
import {AnnotationService} from "../../../../_services/annotation.service";
import {NgbCollapse} from "@ng-bootstrap/ng-bootstrap";
import {ColorscapeService} from "../../../../_services/colorscape.service";
import {ReplaySubject} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {debounceTime, tap} from "rxjs/operators";
import {Breakpoint, UserBreakpoint, UtilityService} from "../../../../shared/_services/utility.service";
import {
  SettingColourPickerComponent
} from "../../../../settings/_components/setting-colour-picker/setting-colour-picker.component";

@Component({
  selector: 'app-highlight-bar',
  imports: [
    TranslocoDirective,
    NgbCollapse,
    SettingColourPickerComponent
  ],
  templateUrl: './highlight-bar.component.html',
  styleUrl: './highlight-bar.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class HighlightBarComponent {

  private readonly annotationService = inject(AnnotationService);
  private readonly colorscapeService = inject(ColorscapeService);
  protected readonly utilityService = inject(UtilityService);
  private readonly destroyRef = inject(DestroyRef);

  isCollapsed = model<boolean>(true);
  isEditMode = model<boolean>(false);

  selectedSlotIndex = model.required<number>();
  @Output() changeSlot = new EventEmitter<number>();
  @Output() changeSlotColor = new EventEmitter<{slot: number, color: string}>();
  slots = this.annotationService.slots;

  slotColor = new ReplaySubject<{slot: number, color: string}>(1);

  selectedSlot = computed(() => {
    const index = this.selectedSlotIndex();
    const slots = this.annotationService.slots();
    if (slots.length === 0 || index >= slots.length) return null;
    return slots[index];
  })

  constructor() {
    this.slotColor.pipe(
      takeUntilDestroyed(this.destroyRef),
      debounceTime(1000),
      tap(val => console.log('Color change: ', val)),
      tap(val => this.changeSlotColor.emit(val))
    ).subscribe();
  }


  selectSlot(index: number, slot: HighlightSlot) {
    this.selectedSlotIndex.set(index);
    this.changeSlot.emit(index);
  }

  updateCollapse(val: boolean) {
    this.isCollapsed.set(val);
  }

  toggleEditMode() {
    const existingEdit = this.isEditMode();
    this.isEditMode.set(!existingEdit);
  }

  handleBackgroundColorChange(color: string) {
    let rgba = color;
    if (color.startsWith('#')) {
      let structrgba = this.colorscapeService.hexToRGBA(color);
      rgba = `rgba(${structrgba.r}, ${structrgba.g}, ${structrgba.b}, ${structrgba.a})`;
    }

    this.slotColor.next({slot: this.selectedSlotIndex(), color: rgba});
  }

  protected readonly Breakpoint = Breakpoint;
  protected readonly UserBreakpoint = UserBreakpoint;
}
