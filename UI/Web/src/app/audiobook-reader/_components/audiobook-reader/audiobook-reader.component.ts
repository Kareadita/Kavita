import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  OnDestroy,
  OnInit,
  signal,
  viewChild
} from '@angular/core';
import {ActivatedRoute, Router} from '@angular/router';
import {ToastrService} from 'ngx-toastr';
import {CHAPTER_ID_DOESNT_EXIST, CHAPTER_ID_NOT_FETCHED, ReaderService} from '../../../_services/reader.service';
import {AccountService} from '../../../_services/account.service';
import {NavService} from '../../../_services/nav.service';
import {AudiobookService} from '../../_services/audiobook.service';
import {AudioInfo} from '../../_models/audio-info';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {translate, TranslocoDirective} from '@jsverse/transloco';
import {ImageService} from '../../../_services/image.service';
import {interval, Subscription} from 'rxjs';

const PLAYBACK_SPEEDS = [0.5, 0.75, 1, 1.25, 1.5, 1.75, 2];
const PROGRESS_SAVE_INTERVAL_MS = 10_000;
const SKIP_SECONDS = 30;

@Component({
  selector: 'app-audiobook-reader',
  standalone: true,
  imports: [TranslocoDirective],
  templateUrl: './audiobook-reader.component.html',
  styleUrls: ['./audiobook-reader.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AudiobookReaderComponent implements OnInit, OnDestroy {

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly readerService = inject(ReaderService);
  private readonly accountService = inject(AccountService);
  private readonly navService = inject(NavService);
  private readonly audiobookService = inject(AudiobookService);
  private readonly imageService = inject(ImageService);
  private readonly toastr = inject(ToastrService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly audioEl = viewChild<ElementRef<HTMLAudioElement>>('audioEl');

  // Route params
  libraryId!: number;
  seriesId!: number;
  chapterId!: number;

  // The image-only auth key is used for media URLs (same mechanism as cover images)
  private readonly apiKey = this.accountService.currentUserImageAuthKey;

  // State
  audioInfo = signal<AudioInfo | null>(null);
  streamUrl = signal('');
  coverUrl = signal('');

  isPlaying = signal(false);
  isLoading = signal(true);
  currentTime = signal(0);
  duration = signal(0);
  volume = signal(1);
  playbackSpeedIndex = signal(2); // index into PLAYBACK_SPEEDS, default 1x
  protected readonly playbackSpeeds = PLAYBACK_SPEEDS;

  prevChapterId = signal(CHAPTER_ID_NOT_FETCHED);
  nextChapterId = signal(CHAPTER_ID_NOT_FETCHED);

  private progressSaveSubscription?: Subscription;

  ngOnInit() {
    this.navService.hideNavBar();

    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      this.libraryId = parseInt(params.get('libraryId') ?? '0', 10);
      this.seriesId = parseInt(params.get('seriesId') ?? '0', 10);
      this.chapterId = parseInt(params.get('chapterId') ?? '0', 10);

      if (!this.chapterId) return;
      this.loadChapter();
    });
  }

  ngOnDestroy() {
    this.navService.showNavBar();
    this.stopProgressSaving();
    this.pauseAndSaveProgress();
  }

  private loadChapter() {
    this.isLoading.set(true);

    this.audiobookService.getAudioInfo(this.chapterId).subscribe({
      next: (info) => {
        this.audioInfo.set(info);
        this.duration.set(info.pages);
        this.coverUrl.set(this.imageService.getChapterCoverImage(this.chapterId));
        this.streamUrl.set(this.audiobookService.getStreamUrl(this.chapterId, this.apiKey() ?? ''));
        this.cdRef.markForCheck();

        // Load saved progress after audio metadata is available
        this.readerService.getProgress(this.chapterId).subscribe(progress => {
          if (progress && progress.pageNum > 0) {
            const audio = this.audioEl()?.nativeElement;
            if (audio) {
              audio.currentTime = progress.pageNum;
            }
            this.currentTime.set(progress.pageNum);
          }
          this.cdRef.markForCheck();
        });

        // Fetch prev/next chapter IDs (requires volumeId from audioInfo)
        this.readerService.getPrevChapter(info.seriesId, info.volumeId, this.chapterId).pipe(
          takeUntilDestroyed(this.destroyRef)
        ).subscribe(id => {
          this.prevChapterId.set(id);
          this.cdRef.markForCheck();
        });

        this.readerService.getNextChapter(info.seriesId, info.volumeId, this.chapterId).pipe(
          takeUntilDestroyed(this.destroyRef)
        ).subscribe(id => {
          this.nextChapterId.set(id);
          this.cdRef.markForCheck();
        });
      },
      error: () => {
        this.toastr.error(translate('audiobook-reader.loading'));
        this.isLoading.set(false);
        this.cdRef.markForCheck();
      }
    });
  }

  onAudioCanPlay() {
    this.isLoading.set(false);
    const audio = this.audioEl()?.nativeElement;
    if (audio) {
      audio.volume = this.volume();
      audio.playbackRate = PLAYBACK_SPEEDS[this.playbackSpeedIndex()];
    }
    this.cdRef.markForCheck();
  }

  onAudioTimeUpdate() {
    const audio = this.audioEl()?.nativeElement;
    if (audio) {
      this.currentTime.set(Math.floor(audio.currentTime));
    }
    this.cdRef.markForCheck();
  }

  onAudioEnded() {
    this.isPlaying.set(false);
    this.stopProgressSaving();
    this.saveProgress();
    // Auto-advance to next chapter
    if (this.nextChapterId() !== CHAPTER_ID_DOESNT_EXIST && this.nextChapterId() !== CHAPTER_ID_NOT_FETCHED) {
      this.navigateToChapter(this.nextChapterId());
    }
    this.cdRef.markForCheck();
  }

  togglePlayPause() {
    const audio = this.audioEl()?.nativeElement;
    if (!audio) return;

    if (this.isPlaying()) {
      audio.pause();
      this.isPlaying.set(false);
      this.stopProgressSaving();
      this.saveProgress();
    } else {
      audio.play().then(() => {
        this.isPlaying.set(true);
        this.startProgressSaving();
        this.cdRef.markForCheck();
      }).catch(() => {
        this.toastr.error(translate('audiobook-reader.loading'));
      });
    }
    this.cdRef.markForCheck();
  }

  skipBack() {
    const audio = this.audioEl()?.nativeElement;
    if (!audio) return;
    audio.currentTime = Math.max(0, audio.currentTime - SKIP_SECONDS);
    this.currentTime.set(Math.floor(audio.currentTime));
    this.cdRef.markForCheck();
  }

  skipForward() {
    const audio = this.audioEl()?.nativeElement;
    if (!audio) return;
    audio.currentTime = Math.min(audio.duration, audio.currentTime + SKIP_SECONDS);
    this.currentTime.set(Math.floor(audio.currentTime));
    this.cdRef.markForCheck();
  }

  seek(event: Event) {
    const input = event.target as HTMLInputElement;
    const audio = this.audioEl()?.nativeElement;
    if (!audio) return;
    const newTime = parseFloat(input.value);
    audio.currentTime = newTime;
    this.currentTime.set(Math.floor(newTime));
    this.cdRef.markForCheck();
  }

  setVolume(event: Event) {
    const input = event.target as HTMLInputElement;
    const v = parseFloat(input.value);
    this.volume.set(v);
    const audio = this.audioEl()?.nativeElement;
    if (audio) audio.volume = v;
    this.cdRef.markForCheck();
  }

  cycleSpeed() {
    const nextIndex = (this.playbackSpeedIndex() + 1) % PLAYBACK_SPEEDS.length;
    this.playbackSpeedIndex.set(nextIndex);
    const audio = this.audioEl()?.nativeElement;
    if (audio) audio.playbackRate = PLAYBACK_SPEEDS[nextIndex];
    this.cdRef.markForCheck();
  }

  get currentSpeed(): number {
    return PLAYBACK_SPEEDS[this.playbackSpeedIndex()];
  }

  navigateToPrev() {
    const prev = this.prevChapterId();
    if (prev !== CHAPTER_ID_DOESNT_EXIST && prev !== CHAPTER_ID_NOT_FETCHED) {
      this.navigateToChapter(prev);
    }
  }

  navigateToNext() {
    const next = this.nextChapterId();
    if (next !== CHAPTER_ID_DOESNT_EXIST && next !== CHAPTER_ID_NOT_FETCHED) {
      this.navigateToChapter(next);
    }
  }

  private navigateToChapter(chapterId: number) {
    this.stopProgressSaving();
    this.router.navigate(['library', this.libraryId, 'series', this.seriesId, 'audio', chapterId]).catch(() => {});
  }

  close() {
    this.stopProgressSaving();
    this.saveProgress();
    this.readerService.closeReader(this.libraryId, this.seriesId, this.chapterId);
  }

  formatTime(seconds: number): string {
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    const s = seconds % 60;
    if (h > 0) {
      return `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
    }
    return `${m}:${String(s).padStart(2, '0')}`;
  }

  get progressPercent(): number {
    const d = this.duration();
    return d > 0 ? (this.currentTime() / d) : 0;
  }

  private saveProgress() {
    const info = this.audioInfo();
    if (!info) return;
    this.readerService.saveProgress(
      this.libraryId, info.seriesId, info.volumeId, this.chapterId, this.currentTime()
    ).subscribe();
  }

  private startProgressSaving() {
    this.stopProgressSaving();
    this.progressSaveSubscription = interval(PROGRESS_SAVE_INTERVAL_MS).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => this.saveProgress());
  }

  private stopProgressSaving() {
    this.progressSaveSubscription?.unsubscribe();
    this.progressSaveSubscription = undefined;
  }

  private pauseAndSaveProgress() {
    const audio = this.audioEl()?.nativeElement;
    if (audio && !audio.paused) {
      audio.pause();
    }
    this.saveProgress();
  }
}
