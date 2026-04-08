import {inject, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from '@jsverse/transloco';
import {RemapRule} from '../_models/reading-list/cbl/remap-rule';
import {CblRemapRuleKind} from '../_models/reading-list/cbl/cbl-remap-rule-kind.enum';
import {LooseLeafOrDefaultNumber, SpecialVolumeNumber} from '../_models/chapter';

@Pipe({
  name: 'cblRemapRuleChapterTitle',
  standalone: true,
  pure: true
})
export class CblRemapRuleChapterTitlePipe implements PipeTransform {
  private readonly translocoService = inject(TranslocoService);

  transform(rule: RemapRule): string {
    if (rule.kind === CblRemapRuleKind.Series) return '';

    const chRange = rule.chapterRange;
    const volNum = rule.volumeNumber;

    if (chRange === LooseLeafOrDefaultNumber + '' && volNum === SpecialVolumeNumber + '') {
      return this.translocoService.translate('entity-title.special');
    }
    if (chRange === LooseLeafOrDefaultNumber + '') {
      return this.translocoService.translate('common.volume-num-shorthand', {num: volNum});
    }
    return this.translocoService.translate('common.issue-num-shorthand', {num: chRange});
  }
}
