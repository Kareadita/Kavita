import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  input,
  model,
  signal,
  ViewChild
} from '@angular/core';
import {ImageService} from "../../../_services/image.service";
import {AccountService} from "../../../_services/account.service";
import {TranslocoDirective} from "@jsverse/transloco";
import {ImageComponent} from "../../../shared/image/image.component";
import {UploadService} from "../../../_services/upload.service";
import {NgxFileDropModule} from "ngx-file-drop";
import {ToastrService} from "ngx-toastr";

interface ImageUploadResult {
  file: File;
  previewUrl: string;
}

@Component({
  selector: 'app-profile-image',
  imports: [
    TranslocoDirective,
    ImageComponent,
    NgxFileDropModule
  ],
  templateUrl: './profile-image.component.html',
  styleUrl: './profile-image.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProfileImageComponent {
  protected readonly imageService = inject(ImageService);
  protected readonly accountService = inject(AccountService);
  private readonly uploadService = inject(UploadService);
  private readonly toastr = inject(ToastrService);

  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;

  userId = input.required<number>();
  showEditButton = input<boolean>(true);

  uploadInProgress = model<boolean>(false);

  selectedFile = signal<File | null>(null);
  previewUrl = signal<string | null>(null);

  acceptableExtensions = ['.png', '.webp', '.jpg', '.jpeg'].join(',');
  maxFileSize = 5 * 1024 * 1024; // 5MB
  imageSelected: ImageUploadResult | null = null;

  canUploadImage = computed(() => {
    return this.accountService.currentUserSignal()?.id === this.userId();
  });

  canDeleteImage = computed(() => {
    return this.accountService.currentUserSignal()?.coverImage && !this.uploadInProgress() && this.showEditButton();
  });

  isImageUploadMode = computed(() => {
    return this.canUploadImage() && !this.uploadInProgress();
  });

  currentImageUrl = model<string | null>(null);

  constructor() {

    effect(() => {
      // Show preview if available, otherwise show existing image
      const userId = this.userId();
      const url =  this.previewUrl() || (userId && this.imageService.getUserCoverImage(userId)) || null;

      this.currentImageUrl.set(url);
    });

  }


  openFileSelector(): void {
    if (!this.uploadInProgress()) {
      this.fileInput.nativeElement.click();
    }
  }

  handleFileInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files[0]) {
      this.processFile(input.files[0]);
    }
    // Reset input value to allow selecting the same file again
    input.value = '';
  }

  restProfileImage() {
    this.uploadService.updateUserCoverImage(this.userId(), '').pipe().subscribe();
  }


  private processFile(file: File): void {
    if (!this.validateFileType(file)) {
      this.toastr.error('Invalid file type. Please select a PNG, WebP, JPG, or JPEG image.');
      return;
    }

    // Validate file size
    if (!this.validateFileSize(file)) {
      this.toastr.error(`File size exceeds ${this.maxFileSize / 1024 / 1024}MB limit.`);
      return;
    }

    this.selectedFile.set(file);
    this.createPreview(file);

    // Emit the file for parent component to handle
    const reader = new FileReader();
    reader.onload = (e) => {
      this.imageSelected = {
        file: file,
        previewUrl: e.target?.result as string
      };
      this.uploadService.updateUserCoverImage(this.userId(), e.target?.result as string).pipe().subscribe();
    };
    reader.readAsDataURL(file);
  }

  /**
   * Creates a preview URL for the selected image
   */
  private createPreview(file: File): void {
    const reader = new FileReader();
    reader.onload = (e) => {
      this.previewUrl.set(e.target?.result as string);
    };
    reader.readAsDataURL(file);
  }

  private validateFileType(file: File): boolean {
    const validTypes = ['image/png', 'image/webp', 'image/jpeg', 'image/jpg'];
    return validTypes.includes(file.type);
  }

  private validateFileSize(file: File): boolean {
    return file.size <= this.maxFileSize;
  }


}
