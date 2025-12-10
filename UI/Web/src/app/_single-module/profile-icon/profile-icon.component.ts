import {ChangeDetectionStrategy, Component, DestroyRef, effect, inject, input, model} from '@angular/core';
import {ImageService} from "../../_services/image.service";
import {ImageComponent} from "../../shared/image/image.component";
import {EVENTS, MessageHubService} from "../../_services/message-hub.service";
import {CoverUpdateEvent} from "../../_models/events/cover-update-event";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

@Component({
  selector: 'app-profile-icon',
  imports: [
    ImageComponent
  ],
  templateUrl: './profile-icon.component.html',
  styleUrl: './profile-icon.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProfileIconComponent {
  protected readonly imageService = inject(ImageService);
  protected readonly hubService = inject(MessageHubService);
  protected readonly destroyRef = inject(DestroyRef);

  userId = input.required<number>();

  size = input<number>(32);
  /**
   * If the image component should respond to cover updates
   */
  processEvents = input<boolean>(true);

  currentImageUrl = model<string>('');

  constructor() {

    this.hubService.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(res => {
      if (!this.processEvents()) return;
      const imageUrl = this.currentImageUrl();

      if (res.event === EVENTS.CoverUpdate) {
        const updateEvent = res.payload as CoverUpdateEvent;
        if (imageUrl === undefined || imageUrl === null || imageUrl === '') return;
        const entityType = this.imageService.getEntityTypeFromUrl(imageUrl);
        if (entityType === updateEvent.entityType) {
          const tokens = imageUrl.split('?')[1].split('&');

          //...seriesId=123&random=
          let id = tokens[0].replace(entityType + 'Id=', '');
          if (id.includes('&')) {
            id = id.split('&')[0];
          }
          if (id === (updateEvent.id + '')) {
            this.currentImageUrl.set(this.imageService.randomize(imageUrl))
          }
        }
      }
    });

    effect(() => {
      const userId = this.userId();

      // Set default image
      this.currentImageUrl.set(this.imageService.getUserCoverImage(userId));
    });

  }

}
