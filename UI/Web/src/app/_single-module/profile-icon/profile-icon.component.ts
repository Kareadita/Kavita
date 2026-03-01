import {ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, signal} from '@angular/core';
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

  private readonly randomSeed = signal<number>(0);
  noImage = signal<boolean>(false);

  currentImageUrl = computed(() => {
    const userId = this.userId();
    const seed = this.randomSeed();
    const url = this.imageService.getUserCoverImage(userId);
    return seed > 0 ? `${url}&random=${seed}` : url;
  });

  constructor() {
    this.hubService.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(res => {
      if (!this.processEvents()) return;
      const imageUrl = this.currentImageUrl();

      if (res.event === EVENTS.CoverUpdate) {
        const updateEvent = res.payload as CoverUpdateEvent;
        if (!imageUrl) return;
        const entityType = this.imageService.getEntityTypeFromUrl(imageUrl);
        if (entityType === updateEvent.entityType) {
          const tokens = imageUrl.split('?')[1].split('&');

          //...seriesId=123&random=
          let id = tokens[0].replace(entityType + 'Id=', '');
          if (id.includes('&')) {
            id = id.split('&')[0];
          }
          if (id === (updateEvent.id + '')) {
            this.noImage.set(false);
            this.randomSeed.update(v => v + 1);
          }
        }
      }
    });
  }

  handleErrorLoad() {
    this.noImage.set(true);
  }

}
