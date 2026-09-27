import {ChangeDetectionStrategy, Component, inject} from '@angular/core';
import {NgStyle} from "@angular/common";
import {NavService} from "../../../_services/nav.service";
import {PRODUCT_SUFFIX} from "../../../branding";

@Component({
    selector: 'app-splash-container',
    templateUrl: './splash-container.component.html',
    styleUrls: ['./splash-container.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [
        NgStyle
    ]
})
export class SplashContainerComponent {
  protected readonly navService = inject(NavService);
  protected readonly productSuffix = PRODUCT_SUFFIX;
}
