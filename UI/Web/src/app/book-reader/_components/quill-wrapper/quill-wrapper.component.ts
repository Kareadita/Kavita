import {ChangeDetectionStrategy, Component, computed, EventEmitter, input, OnInit, Output} from '@angular/core';
import {ContentChange, QuillEditorComponent, QuillFormat} from "ngx-quill";
import {FormGroup, ReactiveFormsModule} from "@angular/forms";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";

export enum QuillTheme {
  Snow = 'snow',
  Bubble = 'bubble',
}

/**
 * Keys for the different options to display in the toolbar
 */
export enum QuillToolbarKey {
  Bold = 'ql-bold',
  Italic = 'ql-italic',
  Underline = 'ql-underline',
  Strikethrough = 'ql-strike',
  Blockquote = 'ql-blockquote',
  CodeBlock = 'ql-code-block',
  Header = 'ql-header',
  List = 'ql-list',
  Script = 'ql-script',
  Indent = 'ql-indent',
  Direction = 'ql-direction',
  FontSize = 'ql-size',
  Color = 'ql-color',
  BackgroundColor = 'ql-background',
  Font = 'ql-font',
  Alignment = 'ql-align',
  EmbedLink = 'ql-link',
  EmbedImage = 'ql-image',
  EmbedVideo = 'ql-video',
  Table = 'ql-table',
  Clean = 'ql-clean',
}

export interface ToolbarItem {
  /**
   * This key is not always unique
   */
  key: QuillToolbarKey;
  /**
   * Value passed to the button itself
   */
  value?: string;
  /**
   * Values used for the select component
   * Pass an **empty** array to use the quill defaults
   */
  values?: string[];
  /**
   * An optional translation key for a tooltip
   */
  tooltipTranslationKey?: string
}

// There is very little documentation to what values are possible.
// https://quilljs.com/docs/modules/toolbar + inspect the editor on that page to figure it out
const defaultToolbarItems: ToolbarItem[][] = [
  [
    {
      key: QuillToolbarKey.FontSize,
      values: [],
    },
    {
      key: QuillToolbarKey.Font,
      values: [],
    },
  ],
  [
    {key: QuillToolbarKey.Bold},
    {key: QuillToolbarKey.Italic},
    {key: QuillToolbarKey.Underline},
    {key: QuillToolbarKey.Strikethrough},
    {key: QuillToolbarKey.List, value: 'bullet'},
    {key: QuillToolbarKey.List, value: 'ordered'},
  ],
  [
    {key: QuillToolbarKey.EmbedLink},
    {key: QuillToolbarKey.EmbedImage},
  ],

  [
    {key: QuillToolbarKey.Clean, tooltipTranslationKey: 'clean-tooltip'},
  ]
];

/**
 * This component is a wrapper around the quill editor for a nicer to use API, and styling that integrates into the
 * Kavita style
 */
@Component({
  selector: 'app-quill-wrapper',
  imports: [
    QuillEditorComponent,
    ReactiveFormsModule,
    NgbTooltip,
    TranslocoDirective,
  ],
  templateUrl: './quill-wrapper.component.html',
  styleUrl: './quill-wrapper.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class QuillWrapperComponent {

  /**
   * The data format used to pass through quill.
   * @default Object
   */
  format = input<QuillFormat>('object');

  /**
   * The quill theme to use
   * @default Snow
   */
  theme = input(QuillTheme.Snow);

  formGroup = input.required<FormGroup>();
  controlName = input.required<string>();

  /**
   * Deligation of the quill onContentChange event
   */
  @Output() contentChanged = new EventEmitter<ContentChange>();

  /**
   * Items to show in the toolbar
   * @default defaultToolbarItems
   */
  toolBarItems = input(defaultToolbarItems);

  /**
   * If not an empty list, only items with their keys present will be shown
   */
  whiteList = input<QuillToolbarKey[]>([]);
  /**
   * Keys in this list will not be shown, unless in the whiteList
   */
  blackList = input<QuillToolbarKey[]>([]);


  toolbar = computed(() => {
    const items = this.toolBarItems();
    const whiteList = this.whiteList();
    const blackList = this.blackList();

    if (whiteList.length === 0 && blackList.length === 0) {
      return items;
    }

    if (whiteList.length > 0) {
      return items
        .map(group => group.filter(item => whiteList.includes(item.key)))
        .filter(group => group.length > 0);
    }

    return items
      .map(group => group.filter(item => !blackList.includes(item.key)))
      .filter(group => group.length > 0);
  });

}
