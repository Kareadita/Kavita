import {
  ChangeDetectionStrategy,
  Component,
  contentChild,
  contentChildren,
  inject,
  input,
  model,
  output,
  TemplateRef
} from '@angular/core';
import {Tabs} from "../../_models/tabs";
import {EditTabDirective} from "../_directive/edit-tab.directive";
import {BreakpointService} from "../../_services/breakpoint.service";
import {FormGroup, ReactiveFormsModule} from "@angular/forms";
import {NgbNav, NgbNavContent, NgbNavItem, NgbNavLink, NgbNavOutlet} from "@ng-bootstrap/ng-bootstrap";
import {NgTemplateOutlet} from "@angular/common";
import {TabTitlePipe} from "../../_pipes/tab-title.pipe";
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-edit-modal-shell',
  imports: [
    NgbNavOutlet,
    NgTemplateOutlet,
    NgbNavItem,
    NgbNav,
    ReactiveFormsModule,
    NgbNavContent,
    NgbNavLink,
    TabTitlePipe,
    TranslocoDirective
  ],
  templateUrl: './edit-modal-shell.component.html',
  styleUrl: './edit-modal-shell.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EditModalShellComponent {
  private readonly breakpointService = inject(BreakpointService);

  translocoPrefix = input.required<string>();
  // eslint-disable-next-line @angular-eslint/no-input-rename
  modalTitle = input.required<string>({ alias: 'title' });
  formGroup = input.required<FormGroup>();

  activeTabId = model<Tabs>();

  save = output<void>();
  // eslint-disable-next-line @angular-eslint/no-output-native
  close = output<void>();
  tabSwitch = output<Tabs>();

  // Automatically tracks projected tabs
  tabs = contentChildren(EditTabDirective);
  /*Optional title for rich content*/
  titleTemplate = contentChild<TemplateRef<any>>('title');

  readonly isMobile = this.breakpointService.isMobile;

  onTabChange(tabId: Tabs, event?: Event) {
    if (event) {
      event.preventDefault();
    }

    if (this.activeTabId() !== tabId) {
      this.activeTabId.set(tabId);
      this.tabSwitch.emit(tabId);
    }
  }
}
