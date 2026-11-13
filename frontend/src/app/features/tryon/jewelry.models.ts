// Role: Types for the virtual try-on jewelry feature, matching the .NET /api/jewelry endpoints.
// imageUrl from the API is relative (for example /jewelry-images/abc.png); JewelryService turns
// it into a full address.

export type JewelryType = 'earring' | 'necklace' | 'ring';

// Legacy ring records remain readable for owner administration, but cannot be selected for upload.
export const JEWELRY_TYPES: readonly { value: JewelryType; label: string; plural: string }[] = [
  { value: 'earring', label: 'Earring', plural: 'Earrings' },
  { value: 'necklace', label: 'Necklace', plural: 'Necklaces' }
];

export interface JewelryItem {
  id: string;
  name: string;
  type: JewelryType;
  imageUrl: string;
  active: boolean;
  createdAt: string;
}

// Same limits as the backend UploadValidator. The backend checks again; this is only for quick feedback.
export const MAX_UPLOAD_BYTES = 5 * 1024 * 1024;
export const MAX_NAME_LENGTH = 60;
