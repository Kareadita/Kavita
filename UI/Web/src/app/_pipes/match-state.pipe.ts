import { Pipe, PipeTransform } from '@angular/core';
import {MatchStateOption} from "../_models/kavitaplus/match-state-option";
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'matchStateOption',
  standalone: true
})
export class MatchStateOptionPipe implements PipeTransform {

  transform(value: MatchStateOption): string {
    switch (value) {
      case MatchStateOption.DontMatch:
        return translate('manage-matched-metadata.dont-match-label');
      case MatchStateOption.All:
        return translate('manage-matched-metadata.all-status-label');
      case MatchStateOption.Matched:
        return translate('manage-matched-metadata.matched-status-label');
      case MatchStateOption.NotMatched:
        return translate('manage-matched-metadata.unmatched-status-label');
      case MatchStateOption.Error:
        return translate('manage-matched-metadata.blacklist-status-label');

    }
  }

}
