import {AbstractControl, FormArray, FormControl, FormGroup} from '@angular/forms';

interface ValidationIssue {
  path: string;
  controlType: string;
  value: any;
  errors: { [key: string]: any } | null;
  status: string;
  disabled: boolean;
}

export function analyzeFormGroupValidation(formGroup: FormGroup, basePath: string = ''): ValidationIssue[] {
  const issues: ValidationIssue[] = [];

  function analyzeControl(control: AbstractControl, path: string): void {
    // Determine control type for better debugging
    let controlType = 'AbstractControl';
    if (control instanceof FormGroup) {
      controlType = 'FormGroup';
    } else if (control instanceof FormArray) {
      controlType = 'FormArray';
    } else if (control instanceof FormControl) {
      controlType = 'FormControl';
    }

    // Add issue if control has validation errors or is invalid
    if (control.invalid || control.errors || control.disabled) {
      issues.push({
        path: path || 'root',
        controlType,
        value: control.value,
        errors: control.errors,
        status: control.status,
        disabled: control.disabled
      });
    }

    // Recursively check nested controls
    if (control instanceof FormGroup) {
      Object.keys(control.controls).forEach(key => {
        const childPath = path ? `${path}.${key}` : key;
        analyzeControl(control.controls[key], childPath);
      });
    } else if (control instanceof FormArray) {
      control.controls.forEach((childControl, index) => {
        const childPath = path ? `${path}[${index}]` : `[${index}]`;
        analyzeControl(childControl, childPath);
      });
    }
  }

  analyzeControl(formGroup, basePath);
  return issues;
}

export function printFormGroupValidation(formGroup: FormGroup, basePath: string = ''): void {
  const issues = analyzeFormGroupValidation(formGroup, basePath);

  console.group(`🔍 FormGroup Validation Analysis (${basePath || 'root'})`);
  console.log(`Overall Status: ${formGroup.status}`);
  console.log(`Overall Valid: ${formGroup.valid}`);
  console.log(`Total Issues Found: ${issues.length}`);

  if (issues.length === 0) {
    console.log('✅ No validation issues found!');
  } else {
    console.log('\n📋 Detailed Issues:');
    issues.forEach((issue, index) => {
      console.group(`${index + 1}. ${issue.path} (${issue.controlType})`);
      console.log(`Status: ${issue.status}`);
      console.log(`Value:`, issue.value);
      console.log(`Disabled: ${issue.disabled}`);

      if (issue.errors) {
        console.log('Validation Errors:');
        Object.entries(issue.errors).forEach(([errorKey, errorValue]) => {
          console.log(`  • ${errorKey}:`, errorValue);
        });
      } else {
        console.log('No specific validation errors (but control is invalid)');
      }
      console.groupEnd();
    });
  }

  console.groupEnd();
}

// Alternative function that returns a formatted string instead of console logging
export function getFormGroupValidationReport(formGroup: FormGroup, basePath: string = ''): string {
  const issues = analyzeFormGroupValidation(formGroup, basePath);

  let report = `FormGroup Validation Report (${basePath || 'root'})\n`;
  report += `Overall Status: ${formGroup.status}\n`;
  report += `Overall Valid: ${formGroup.valid}\n`;
  report += `Total Issues Found: ${issues.length}\n\n`;

  if (issues.length === 0) {
    report += '✅ No validation issues found!';
  } else {
    report += 'Detailed Issues:\n';
    issues.forEach((issue, index) => {
      report += `\n${index + 1}. ${issue.path} (${issue.controlType})\n`;
      report += `   Status: ${issue.status}\n`;
      report += `   Value: ${JSON.stringify(issue.value)}\n`;
      report += `   Disabled: ${issue.disabled}\n`;

      if (issue.errors) {
        report += '   Validation Errors:\n';
        Object.entries(issue.errors).forEach(([errorKey, errorValue]) => {
          report += `     • ${errorKey}: ${JSON.stringify(errorValue)}\n`;
        });
      } else {
        report += '   No specific validation errors (but control is invalid)\n';
      }
    });
  }

  return report;
}
