import {ChangeDetectionStrategy, Component, computed, inject, input, model} from '@angular/core';
import {Annotation} from "../../../_models/annotations/annotation";
import {AnnotationService} from "../../../../_services/annotation.service";
import {AccountService} from "../../../../_services/account.service";
import {tap} from "rxjs/operators";
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-annotation-likes',
  imports: [
    TranslocoDirective
  ],
  templateUrl: './annotation-likes.component.html',
  styleUrl: './annotation-likes.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AnnotationLikesComponent {

  private readonly annotationService = inject(AnnotationService);
  protected readonly accountService = inject(AccountService)

  /**
   * If the element should be shown
   */
  visible = input.required<boolean>();

  /**
   * The annotation for which the likes are shown. Will emit a annotationChange when the likes update
   */
  annotation = model.required<Annotation>();

  liked = computed(() => this.annotation().likes.includes(this.accountService.userId() ?? 0));

  handleLikeChange() {
    const userId = this.accountService.userId();
    if (!userId) return;

    if (this.annotation().ownerUserId ===userId) return;

    const sub$ = this.liked()
      ? this.annotationService.unLikeAnnotations([this.annotation().id])
      : this.annotationService.likeAnnotations([this.annotation().id]);

    sub$.pipe(
      tap(() => {
        this.annotation.update(x => {
          const newLikes = this.liked()
            ? x.likes.filter(id => id !== userId)
            : [...x.likes, userId];

          return {
            ...x,
            likes: newLikes,
          }
        })
      })
    ).subscribe();
  }

}
