// Role: A small shared type describing data that is loading, ready, or failed.

export type Loadable<T> =
  | { status: 'loading' }
  | { status: 'ready'; value: T }
  | { status: 'error'; message: string };
