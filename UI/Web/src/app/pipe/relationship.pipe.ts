import { Pipe, PipeTransform } from '@angular/core';
import { RelationKind } from '../_models/series-detail/relation-kind';

@Pipe({
  name: 'relationship'
})
export class RelationshipPipe implements PipeTransform {

  transform(relationship: RelationKind | undefined): string {
    if (relationship === undefined) return '';
    switch (relationship) {
      case RelationKind.Adaptation:
        return 'Adaptaion';
      case RelationKind.AlternativeSetting:
        return 'Alternative Setting';
      case RelationKind.AlternativeVersion:
        return 'Alternative Version';
      case RelationKind.Character:
        return 'Character';
      case RelationKind.Contains:
        return 'Contains';
      case RelationKind.Doujinshi:
        return 'Doujinshi';
      case RelationKind.Other:
        return 'Other';
      case RelationKind.Prequel:
        return 'Prequel';
      case RelationKind.Sequel:
        return 'Sequel';
      case RelationKind.SideStory:
        return 'Side Story';
      case RelationKind.SpinOff:
        return 'Spin Off';
      default:
        return '';
    }
  }

}
