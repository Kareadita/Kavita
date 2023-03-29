import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { FilterComparison } from '../../_models/filter-comparison';
import { FilterField, allFields } from '../../_models/filter-field';
import { FilterStatement } from '../../_models/filter-statement';

@Component({
  selector: 'app-series-name-filter',
  templateUrl: './series-name-filter.component.html',
  styleUrls: ['./series-name-filter.component.scss']
})
export class SeriesNameFilterComponent implements OnInit {

  @Input() preset: FilterField = FilterField.SeriesName;
  @Input() disabled: boolean = false;
  @Output() filterStatement = new EventEmitter<FilterStatement>();

  formGroup: FormGroup = new FormGroup({
    'comparison': new FormControl<FilterComparison>(FilterComparison.Equal, []),
    'filterValue': new FormControl<string>('', []),

  });
  tooltipHelp: string = 'Series name will filter against Name, Sort Name, or Localized Name';
  validComprisons = [
    FilterComparison.Equal,
    FilterComparison.NotEqual,
    FilterComparison.BeginsWith,
    FilterComparison.EndsWith,
    FilterComparison.Matches
  ];
  allFields = allFields;
  

  get IsTextInput() {
    return [FilterField.SeriesName, FilterField.Summary].includes(this.formGroup.get('input')?.value!);
  }

  get IsNumberInput() {
    return [FilterField.ReadTime, FilterField.ReleaseYear, FilterField.AgeRating, FilterField.ReadProgress, FilterField.UserRating].includes(this.formGroup.get('input')?.value!);
  }

  constructor() {}

  ngOnInit() {

    console.log('All Filter Fields: ', this.allFields);
    this.formGroup.addControl('input', new FormControl(this.preset, []));

    this.formGroup.valueChanges.subscribe(_ => {
      this.filterStatement.emit({
        comparison: this.formGroup.get('comparison')?.value!,
        field: FilterField.SeriesName,
        value: this.formGroup.get('input')?.value!
      });
    });
  }

  remove() {

  }

}
