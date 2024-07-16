import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ContentChild, EventEmitter,
  inject,
  Input, Output,
  TemplateRef
} from '@angular/core';
import {TranslocoDirective} from "@ngneat/transloco";
import {NgTemplateOutlet} from "@angular/common";

@Component({
  selector: 'app-setting-item',
  standalone: true,
  imports: [
    TranslocoDirective,
    NgTemplateOutlet
  ],
  templateUrl: './setting-item.component.html',
  styleUrl: './setting-item.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SettingItemComponent {

  private readonly cdRef = inject(ChangeDetectorRef);

  @Input({required:true}) title: string = '';
  @Input() canEdit: boolean = true;
  @Input() isEditMode: boolean = false;
  @Input() subtitle: string | undefined = undefined;
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

  toggleEditMode() {
    this.isEditMode = !this.isEditMode;
    this.editMode.emit(this.isEditMode);
    this.cdRef.markForCheck();
  }

}
