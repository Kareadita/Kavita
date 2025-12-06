import {ChangeDetectionStrategy, Component, effect, inject, input, model} from '@angular/core';
import {ImageService} from "../../_services/image.service";
import {ImageComponent} from "../../shared/image/image.component";
import {EVENTS, MessageHubService} from "../../_services/message-hub.service";
import {CoverUpdateEvent} from "../../_models/events/cover-update-event";

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

  userId = input.required<number>();

  size = input<number>(32);
  /**
   * If the image component should respond to cover updates
   */
  processEvents = input<boolean>(true);

  currentImageUrl = model<string>('');


  // currentImageUrl = computed(() => {
  //   const userId = this.userId();
  //   return this.imageService.getUserCoverImage(userId);
  // });

  constructor() {


    effect(() => {
      const userId = this.userId();
      const res = this.hubService.messageSignal();

      // Set default image
      this.currentImageUrl.set(this.imageService.getUserCoverImage(userId));

      if (res?.event !== EVENTS.CoverUpdate) return;

      const updateEvent = res.payload as CoverUpdateEvent;
      const imageUrl = this.currentImageUrl();
      if (imageUrl === undefined || imageUrl === null || imageUrl === '') return;
      const entityType = this.imageService.getEntityTypeFromUrl(imageUrl);
      if (entityType === updateEvent.entityType) {
        const tokens = imageUrl.split('?')[1].split('&');

        //...userId=123&random=
        let id = tokens[0].replace(entityType + 'Id=', '');
        if (id.includes('&')) {
          id = id.split('&')[0];
        }
        if (id === (updateEvent.id + '')) {
          this.currentImageUrl.set(this.imageService.randomize(imageUrl));
        }
      }
    });

  }

}
