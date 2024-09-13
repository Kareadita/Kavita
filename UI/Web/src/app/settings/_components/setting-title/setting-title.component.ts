import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, ContentChild,
  EventEmitter,
  inject,
  Input,
  Output, TemplateRef
} from '@angular/core';
import {NgTemplateOutlet} from "@angular/common";
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-setting-title',
  standalone: true,
  imports: [
    NgTemplateOutlet,
    TranslocoDirective
  ],
  templateUrl: './setting-title.component.html',
  styleUrl: './setting-title.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SettingTitleComponent {

  private readonly cdRef = inject(ChangeDetectorRef);

  @Input({required:true}) title: string = '';
  @Input() id: string | undefined = undefined;
  @Input() canEdit: boolean = true;
  @Input() isEditMode: boolean = false;
  @Output() editMode = new EventEmitter<boolean>();
  @ContentChild('extra') titleExtraRef!: TemplateRef<any>;

  toggleViewMode() {
    this.isEditMode = !this.isEditMode;
    this.editMode.emit(this.isEditMode);
    this.cdRef.markForCheck();
  }

}
