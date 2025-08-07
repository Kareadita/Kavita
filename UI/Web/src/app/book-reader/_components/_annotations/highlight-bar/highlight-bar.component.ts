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
import {HighlightSlot, RgbaColor} from "../../../_models/annotations/highlight-slot";
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
import {Color} from "@iplab/ngx-color-picker";
import {AccountService} from "../../../../_services/account.service";

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

  selectedSlotIndex = model.required<number>();
  isCollapsed = model<boolean>(true);
  canCollapse = model<boolean>(true);
  isEditMode = model<boolean>(false);

  slots = this.annotationService.slots;

  selectedSlot = computed(() => {
    const index = this.selectedSlotIndex();
    const slots = this.annotationService.slots();
    if (slots.length === 0 || index >= slots.length) return null;
    return slots[index];
  })


  selectSlot(index: number, slot: HighlightSlot) {
    this.selectedSlotIndex.set(index);
  }

  updateCollapse(val: boolean) {
    this.isCollapsed.set(val);
  }

  toggleEditMode() {
    const existingEdit = this.isEditMode();
    this.isEditMode.set(!existingEdit);
  }

  handleSlotColourChange(index: number, color: RgbaColor) {
    this.annotationService.updateSlotColor(index, color).subscribe();
  }

  protected readonly Breakpoint = Breakpoint;
  protected readonly UserBreakpoint = UserBreakpoint;
}
