// Role: Catches every failed HTTP call and replaces the raw error with a
// friendly message that screens can show to the user.

import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

export class ApiError extends Error {
  constructor(message: string, readonly status: number) {
    super(message);
    this.name = 'ApiError';
  }
}

// The API sends plain text, or { "message": "..." } (try-on jewelry), for 400 and 422 responses.
// Use it when it is short.
function serverText(error: HttpErrorResponse): string | null {
  const body: unknown = error.error;
  const text = typeof body === 'string' ? body
    : body !== null && typeof body === 'object' && typeof (body as { message?: unknown }).message === 'string' ? (body as { message: string }).message
    : null;
  return text !== null && text.length > 0 && text.length < 200 ? text : null;
}

export function toFriendlyMessage(error: HttpErrorResponse): string {
  switch (error.status) {
    case 0:
      return 'Cannot reach the server. Check that the API is running and try again.';
    case 400:
      return serverText(error) ?? 'The request was not valid. Please check the details and try again.';
    case 404:
      return 'That record was not found. It may have been removed.';
    case 422:
      return serverText(error) ?? 'The request could not be processed.';
    default:
      return error.status >= 500
        ? 'The server had a problem. Please try again in a moment.'
        : 'Something went wrong. Please try again.';
  }
}

export const errorInterceptor: HttpInterceptorFn = (request, next) =>
  next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        return throwError(() => new ApiError(toFriendlyMessage(error), error.status));
      }
      return throwError(() => new ApiError('Something went wrong. Please try again.', -1));
    })
  );
