import {ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, input, signal} from '@angular/core';
import {ImageComponent} from "../../shared/image/image.component";
import {FilterField} from "../../_models/metadata/v2/filter-field";
import {Person} from "../../_models/metadata/person";
import {ImageService} from "../../_services/image.service";
import {FilterComparison} from "../../_models/metadata/v2/filter-comparison";
import {FilterUtilitiesService} from "../../shared/_services/filter-utilities.service";

const ANIMATION_TIME = 3000;

@Component({
    selector: 'app-publisher-flipper',
    imports: [
        ImageComponent
    ],
    templateUrl: './publisher-flipper.component.html',
    styleUrl: './publisher-flipper.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class PublisherFlipperComponent {

  protected readonly imageService = inject(ImageService);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly destroyRef = inject(DestroyRef);

  publishers = input<Person[]>([]);


  currentIndex = signal(0);
  isFlipped = signal(false);

  currentPublisher = computed(() => {
    const publishers = this.publishers();
    if (publishers.length === 0) return undefined;
    return publishers[this.currentIndex() % publishers.length];
  });

  nextPublisher = computed(() => {
    const publishers = this.publishers();
    if (publishers.length === 0) return undefined;
    if (publishers.length === 1) return publishers[0];
    return publishers[(this.currentIndex() + 1) % publishers.length];
  });

  constructor() {
    // Start flipping when publishers has more than 1 item
    effect(() => {
      const publishers = this.publishers();
      if (publishers.length <= 1) return;

      const intervalId = setInterval(() => {
        this.isFlipped.update(v => !v);

        // Advance index on every other toggle (when flipping back)
        if (!this.isFlipped()) {
          this.currentIndex.update(i => (i + 1) % publishers.length);
        }
      }, ANIMATION_TIME);

      this.destroyRef.onDestroy(() => clearInterval(intervalId));
    });
  }

  openPublisher(filter: string | number) {
    // TODO: once we build out publisher person-detail page, we can redirect there
    this.filterUtilityService.applyFilter(['all-series'], FilterField.Publisher, FilterComparison.Equal, `${filter}`).subscribe();
  }
}
