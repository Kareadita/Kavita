import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  EventEmitter,
  inject,
  Input,
  OnInit,
  Output
} from '@angular/core';
import {NgClass} from "@angular/common";
import {TranslocoDirective} from "@jsverse/transloco";
import {Breakpoint, UtilityService} from "../../shared/_services/utility.service";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {Action, ActionItem} from "../../_services/action-factory.service";
import {AccountService} from "../../_services/account.service";
import {tap} from "rxjs";
import {User} from "../../_models/user";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

@Component({
  selector: 'app-actionable-modal',
  standalone: true,
  imports: [
    NgClass,
    TranslocoDirective
  ],
  templateUrl: './actionable-modal.component.html',
  styleUrl: './actionable-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ActionableModalComponent implements OnInit {

  protected readonly utilityService = inject(UtilityService);
  protected readonly modal = inject(NgbActiveModal);
  protected readonly accountService = inject(AccountService);
  protected readonly cdRef = inject(ChangeDetectorRef);
  protected readonly destroyRef = inject(DestroyRef);
  protected readonly Breakpoint = Breakpoint;

  @Input() actions: ActionItem<any>[] = [];
  @Input() willRenderAction!: (action: ActionItem<any>) => boolean;
  @Input() shouldRenderSubMenu!: (action: ActionItem<any>, dynamicList: null | Array<any>) => boolean;
  @Output() actionPerformed = new EventEmitter<ActionItem<any>>();

  currentLevel: string[] = [];
  currentItems: ActionItem<any>[] = [];
  user!: User | undefined;

  ngOnInit() {
    this.currentItems = this.actions;
    this.accountService.currentUser$.pipe(tap(user => {
      this.user = user;
      this.cdRef.markForCheck();
    }), takeUntilDestroyed(this.destroyRef)).subscribe();
  }

  handleItemClick(item: ActionItem<any>) {
    if (item.children && item.children.length > 0) {
      this.currentLevel.push(item.title);
      this.currentItems = item.children;
    } else if (item.dynamicList) {
      item.dynamicList.subscribe(dynamicItems => {
        this.currentLevel.push(item.title);
        this.currentItems = dynamicItems.map(di => ({
          ...item,
          title: di.title,
          _extra: di
        }));
      });
    } else {
      this.actionPerformed.emit(item);
      this.modal.close(item);
    }
    this.cdRef.markForCheck();
  }

  handleBack() {
    if (this.currentLevel.length > 0) {
      this.currentLevel.pop();

      let items = this.actions;
      for (let level of this.currentLevel) {
        items = items.find(item => item.title === level)?.children || [];
      }

      this.currentItems = items;
      this.cdRef.markForCheck();
    }
  }

  // willRenderAction(action: ActionItem<any>) {
  //   if (this.user === undefined) return false;
  //
  //   return this.accountService.canInvokeAction(this.user, action.action);
  // }

}
