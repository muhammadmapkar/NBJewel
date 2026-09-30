// Role: Shared option lists and labels used by the item detail and item form screens.

export const MAKING_TYPES: { value: string; label: string }[] = [
  { value: 'perGram', label: 'Per gram' },
  { value: 'percent', label: 'Percent of metal value' },
  { value: 'flat', label: 'Flat amount' }
];

export function makingTypeLabel(value: string): string {
  return MAKING_TYPES.find(t => t.value.toLowerCase() === value.toLowerCase())?.label ?? value;
}

// Reads the message from an error thrown by the HTTP error interceptor.
export function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : 'Something went wrong. Please try again.';
}
