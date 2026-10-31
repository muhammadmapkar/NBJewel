// Role: Loads MediaPipe Tasks Vision in the browser and returns face or hand landmarks for each
// video frame. Everything runs on the phone; no video leaves it.
// Files: the wasm runtime is copied from node_modules/@mediapipe/tasks-vision (pinned in
// package.json) into the build at /mediapipe/wasm (see angular.json). The two model files load
// from Google's pinned model URLs below (float16, version 1). The library itself is loaded only
// when the camera page opens.

import type { FaceLandmarker, HandLandmarker } from '@mediapipe/tasks-vision';
import { Point } from './overlay-math';

export const FACE_MODEL_URL = 'https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task';
export const HAND_MODEL_URL = 'https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task';

export type TrackerKind = 'face' | 'hand';

export interface LandmarkTracker {
  // Normalized landmarks (0 to 1) of the first face or hand, or null when none is found.
  detect(video: HTMLVideoElement, timestampMs: number): Point[] | null;
  close(): void;
}

function wasmPath(): string {
  return new URL('mediapipe/wasm', document.baseURI).href;
}

// Frames with nothing found on the GPU before the CPU is also tried (about 2 seconds).
const GPU_EMPTY_FRAMES = 60;

type Task = FaceLandmarker | HandLandmarker;

export async function createTracker(kind: TrackerKind): Promise<LandmarkTracker> {
  const vision = await import('@mediapipe/tasks-vision');
  const files = await vision.FilesetResolver.forVisionTasks(wasmPath());
  const make = (delegate: 'GPU' | 'CPU'): Promise<Task> => kind === 'face'
    ? vision.FaceLandmarker.createFromOptions(files, {
      baseOptions: { modelAssetPath: FACE_MODEL_URL, delegate },
      runningMode: 'VIDEO', numFaces: 1, minFaceDetectionConfidence: 0.5, minTrackingConfidence: 0.5
    })
    : vision.HandLandmarker.createFromOptions(files, {
      baseOptions: { modelAssetPath: HAND_MODEL_URL, delegate },
      runningMode: 'VIDEO', numHands: 1, minHandDetectionConfidence: 0.5, minTrackingConfidence: 0.5
    });
  const run = (task: Task, video: HTMLVideoElement, timestampMs: number): Point[] | null => kind === 'face'
    ? (task as FaceLandmarker).detectForVideo(video, timestampMs).faceLandmarks[0] ?? null
    : (task as HandLandmarker).detectForVideo(video, timestampMs).landmarks[0] ?? null;

  // GPU is faster, but on some phones and browsers it fails, either at start (then the CPU is
  // used) or silently by finding nothing. For the silent case, after GPU_EMPTY_FRAMES empty frames
  // a CPU copy is loaded and tried on every 4th empty frame; if it finds something, it takes over
  // for good.
  let gpu: Task | null = null;
  let cpu: Task | null = null;
  try { gpu = await make('GPU'); } catch { cpu = await make('CPU'); }
  let gpuEmpty = 0;
  let cpuLoading = false;
  let cpuTries = 0;

  return {
    detect(video, timestampMs) {
      if (!gpu) return cpu ? run(cpu, video, timestampMs) : null;
      const found = run(gpu, video, timestampMs);
      if (found) { gpuEmpty = 0; return found; }
      if (++gpuEmpty < GPU_EMPTY_FRAMES) return null;
      if (!cpu) {
        if (!cpuLoading) {
          cpuLoading = true;
          make('CPU').then(task => { cpu = task; }).catch(() => { /* keep the GPU only */ });
        }
        return null;
      }
      // While nothing is in view, try the CPU only on every 4th frame, so searching does not
      // double the work for the rest of the session.
      if (++cpuTries % 4 !== 0) return null;
      const fromCpu = run(cpu, video, timestampMs);
      if (fromCpu) { gpu.close(); gpu = null; }
      return fromCpu;
    },
    close() { gpu?.close(); cpu?.close(); gpu = null; cpu = null; }
  };
}
