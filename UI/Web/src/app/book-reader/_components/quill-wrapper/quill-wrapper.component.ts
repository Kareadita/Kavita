import {ChangeDetectionStrategy, Component, computed, EventEmitter, input, OnInit, Output} from '@angular/core';
import {ContentChange, QuillEditorComponent, QuillFormat} from "ngx-quill";
import {FormGroup, ReactiveFormsModule} from "@angular/forms";

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
  CodeBlock = 'ql-blockquote',
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
   */
  values?: string[];
}

const DefaultToolbarItems: ToolbarItem[] = [
  {key: QuillToolbarKey.Bold},
  {key: QuillToolbarKey.Italic},
  {key: QuillToolbarKey.Underline},
  {key: QuillToolbarKey.Strikethrough},
  {
    key: QuillToolbarKey.FontSize,
    values: ['small', '', 'large', 'huge'],
  },
  {
    key: QuillToolbarKey.Font,
    values: ['', 'serif', 'monospace'],
  }
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
  ],
  templateUrl: './quill-wrapper.component.html',
  styleUrl: './quill-wrapper.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class QuillWrapperComponent implements OnInit {

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
   * @default DefaultToolbarItems
   */
  toolBarItems = input(DefaultToolbarItems);

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
      return items.filter(item => whiteList.includes(item.key));
    }

    return items.filter(item => !blackList.includes(item.key));
  });

  ngOnInit() {
    console.log("Init Quil Wrapper")
  }


}
