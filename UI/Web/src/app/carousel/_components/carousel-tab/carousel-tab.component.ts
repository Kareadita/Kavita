import {ChangeDetectionStrategy, Component, ContentChild, Input, TemplateRef} from '@angular/core';
import {TabId} from "../carousel-tabs/carousel-tabs.component";

@Component({
  selector: 'app-carousel-tab',
  standalone: true,
  imports: [],
  templateUrl: './carousel-tab.component.html',
  styleUrl: './carousel-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CarouselTabComponent {

  @Input({required: true}) id!: TabId;
  @ContentChild(TemplateRef, {static: true}) implicitContent!: TemplateRef<any>;

}
