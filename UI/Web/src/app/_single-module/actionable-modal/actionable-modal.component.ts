import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  EventEmitter,
  inject,
  Input,
  OnInit,
  Output
} from '@angular/core';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {Breakpoint, UtilityService} from "../../shared/_services/utility.service";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {Action, ActionableEntity, ActionItem} from "../../_services/action-factory.service";
import {AccountService} from "../../_services/account.service";
import {tap} from "rxjs";
import {User} from "../../_models/user/user";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

@Component({
    selector: 'app-actionable-modal',
    imports: [
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

  @Input() entity: ActionableEntity = null;
  @Input() actions: ActionItem<any>[] = [];
  @Input() willRenderAction!: (action: ActionItem<any>, user: User) => boolean;
  @Input() shouldRenderSubMenu!: (action: ActionItem<any>, dynamicList: null | Array<any>) => boolean;
  @Output() actionPerformed = new EventEmitter<ActionItem<any>>();

  currentLevel: string[] = [];
  currentItems: ActionItem<any>[] = [];
  user!: User | undefined;

  ngOnInit() {
    // Copy as the list may be shared between entities
    const actionItems = this.actions.map(action => this.utilityService.copyActionItem(action));

    // On Mobile, surface download
    const otherActionIndex = actionItems.findIndex(i => i.action === Action.Submenu && i.title === 'others')
    if (otherActionIndex >= 0) {
      const downloadActionIndex = actionItems[otherActionIndex].children.findIndex(a => a.action === Action.Download);

      if (downloadActionIndex >= 0) {
        const downloadAction = actionItems[otherActionIndex].children.splice(downloadActionIndex, 1)[0];
        actionItems.push(downloadAction);

        // Check if Other has any other children, else remove
        if (actionItems[otherActionIndex].children.length === 0) {
          actionItems.splice(otherActionIndex, 1);
        }
      }
    }

    this.actions = actionItems;
    this.currentItems = this.translateOptions(this.actions)

    this.accountService.currentUser$.pipe(tap(user => {
      this.user = user;
      this.cdRef.markForCheck();
    }), takeUntilDestroyed(this.destroyRef)).subscribe();
  }

  handleItemClick(item: ActionItem<any>) {
    if (item.children && item.children.length > 0) {
      this.currentLevel.push(item.title);

      if (item.children.length === 1 && item.children[0].dynamicList) {
        item.children[0].dynamicList.subscribe(dynamicItems => {
          this.currentItems = dynamicItems.map(di => ({
            ...item,
            children: [], // Required as dynamic list is only one deep
            title: di.title,
            _extra: di,
            action: item.children[0].action // override action to be correct from child
          }));
        });
      } else {
        this.currentItems = this.translateOptions(item.children);
      }
    }
    else {
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

      this.currentItems = this.translateOptions(items);
      this.cdRef.markForCheck();
    }
  }

  translateOptions(opts: Array<ActionItem<any>>) {
    return opts.map(a => {
      return {...a, title: translate('actionable.' + a.title)};
    })
  }

}
