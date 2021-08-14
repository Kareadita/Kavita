import { Component, EventEmitter, Input, OnDestroy, OnInit, Output } from '@angular/core';
import { FormBuilder, FormControl, FormGroup } from '@angular/forms';
import { NgxFileDropEntry, FileSystemFileEntry } from 'ngx-file-drop';
import { fromEvent, Subject } from 'rxjs';
import { takeWhile } from 'rxjs/operators';
import { ToastrService } from 'ngx-toastr';
import { ImageService } from 'src/app/_services/image.service';
import { KEY_CODES } from 'src/app/shared/_services/utility.service';

@Component({
  selector: 'app-cover-image-chooser',
  templateUrl: './cover-image-chooser.component.html',
  styleUrls: ['./cover-image-chooser.component.scss']
})
export class CoverImageChooserComponent implements OnInit, OnDestroy {

  @Input() imageUrls: Array<string> = [];
  @Output() imageUrlsChange: EventEmitter<Array<string>> = new EventEmitter<Array<string>>();

  /**
   * Should the control give the ability to select an image that emits the reset status for cover image
   */
  @Input() showReset: boolean = false;
  @Output() resetClicked: EventEmitter<void> = new EventEmitter<void>();

  /**
   * Emits the selected index. Used usually to check if something other than the default image was selected.
   */
  @Output() imageSelected: EventEmitter<number> = new EventEmitter<number>();
  /**
   * Emits a base64 encoded image
   */
  @Output() selectedBase64Url: EventEmitter<string> = new EventEmitter<string>();



  selectedIndex: number = 0;
  form!: FormGroup;
  files: NgxFileDropEntry[] = [];

  mode: 'file' | 'url' | 'all' = 'all';
  private readonly onDestroy = new Subject<void>();

  constructor(public imageService: ImageService, private fb: FormBuilder, private toastr: ToastrService) { }

  ngOnInit(): void {
    this.form = this.fb.group({
      coverImageUrl: new FormControl('', [])
    });
  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  getBase64Image(img: HTMLImageElement) {
    const canvas = document.createElement("canvas");
    canvas.width = img.width;
    canvas.height = img.height;
    const ctx = canvas.getContext("2d", {alpha: false});
    if (!ctx) {
      return '';
    }

    ctx.drawImage(img, 0, 0);
    var dataURL = canvas.toDataURL("image/png");
    return dataURL;
  }

  selectImage(index: number) {
    if (this.selectedIndex === index) { return; }
    this.selectedIndex = index;
    this.imageSelected.emit(this.selectedIndex);
    const selector = `.chooser img[src="${this.imageUrls[this.selectedIndex]}"]`;

    
    const elem = document.querySelector(selector) || document.querySelectorAll('.chooser img.card-img-top')[this.selectedIndex];
    if (elem) {
      const imageElem = <HTMLImageElement>elem;
      if (imageElem.src.startsWith('data')) {
        this.selectedBase64Url.emit(imageElem.src);
        return;
      }
      const image = this.getBase64Image(imageElem);
      if (image != '') {
        this.selectedBase64Url.emit(image);
      }
    }
  }

  loadImage() {
    const url = this.form.get('coverImageUrl')?.value.trim();
    if (url && url != '') {
      const img = new Image();
      img.crossOrigin = 'Anonymous';
      img.src = this.form.get('coverImageUrl')?.value;
      img.onload = (e) => this.handleUrlImageAdd(e);
      img.onerror = (e) => {
        this.toastr.error('The image could not be fetched due to server refusing request. Please download and upload from file instead.');
        this.form.get('coverImageUrl')?.setValue('');  
      }
      this.form.get('coverImageUrl')?.setValue('');
    }
  }
  


  

  public dropped(files: NgxFileDropEntry[]) {
    this.files = files;
    for (const droppedFile of files) {

      // Is it a file?
      if (droppedFile.fileEntry.isFile) {
        const fileEntry = droppedFile.fileEntry as FileSystemFileEntry;
        fileEntry.file((file: File) => {
          const reader  = new FileReader();
          reader.onload = (e) => this.handleFileImageAdd(e);
          reader.readAsDataURL(file);
        });
      }
    }
  }

  handleFileImageAdd(e: any) {
    if (e.target == null) return;

    this.imageUrls.push(e.target.result);
    this.imageUrlsChange.emit(this.imageUrls);
    this.selectedIndex += 1;
    this.imageSelected.emit(this.selectedIndex); // Auto select newly uploaded image
    this.selectedBase64Url.emit(e.target.result);
  }

  handleUrlImageAdd(e: any) {
    console.log(e);
    if (e.path === null || e.path.length === 0) return;

    const url = this.getBase64Image(e.path[0]);
    this.imageUrls.push(url);
    this.imageUrlsChange.emit(this.imageUrls);

    setTimeout(() => {
      // Auto select newly uploaded image and tell parent of new base64 url
      this.selectImage(this.selectedIndex + 1)
    });
  }

  public fileOver(event: any){
  }

  public fileLeave(event: any){
  }

  reset() {
    this.resetClicked.emit();
    this.selectedIndex = -1;
  }

  setupEnterHandler() {
    setTimeout(() => {
      const elem = document.querySelector('input[id="load-image"]');
      if (elem == null) return;
      fromEvent(elem, 'keydown')
        .pipe(takeWhile(() => this.mode === 'url')).subscribe((event) => {
          const evt = <KeyboardEvent>event;
          switch(evt.key) {
            case KEY_CODES.ENTER:
            {
              this.loadImage();
              break;
            }
      
            case KEY_CODES.ESC_KEY:
              this.mode = 'all';
              event.stopPropagation();
              break;
            default:
              break;
          }
        });
    });
  }

}
