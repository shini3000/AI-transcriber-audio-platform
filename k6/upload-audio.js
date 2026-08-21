import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Rate, Counter } from 'k6/metrics';

const audioFile = open('./en-i-love-my-family-69.mp3', 'b');

const uploadDuration = new Trend('audio_upload_duration_ms');
const uploadSuccessRate = new Rate('audio_upload_success_rate');
const failedUploads = new Counter('audio_upload_failures');

export const options = {
  stages: [
    { duration: '15s', target: 5 },
    { duration: '30s', target: 10 },
    { duration: '15s', target: 25 },
    { duration: '15s', target: 0 },
  ],
  thresholds: {
    http_req_duration: ['p(95)<10000'],
    audio_upload_success_rate: ['rate>0.95'],
    http_req_failed: ['rate<0.05'],
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5069';

export default function () {
  const url = `${BASE_URL}/api/audio/upload`;

  const uniqueFileName = `test-vu${__VU}-iter${__ITER}-${Date.now()}.mp3`;

  const payload = {
    file: http.file(audioFile, uniqueFileName, 'audio/mpeg'),
  };

  const params = {
    tags: { name: 'UploadAudio' },
    timeout: '30s',
  };

  const startTime = Date.now();
  const res = http.post(url, payload, params);
  const duration = Date.now() - startTime;

  uploadDuration.add(duration);

  const isSuccess = check(res, {
    'status is 201 Created': (r) => r.status === 201,
    'response has audio id': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body && typeof body.id === 'string' && body.id.length > 0;
      } catch {
        return false;
      }
    },
    'response has storageUrl': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body && typeof body.storageUrl === 'string';
      } catch {
        return false;
      }
    },
  });

  if (isSuccess) {
    uploadSuccessRate.add(1);
  } else {
    uploadSuccessRate.add(0);
    failedUploads.add(1);
    console.error(`[VU ${__VU}] Upload failed with status ${res.status}: ${res.body}`);
  }

  sleep(0.5 + Math.random());
}
