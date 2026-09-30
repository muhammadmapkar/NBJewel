// Role: Describes one image of an item.
// Assumed, no backend endpoint yet: the .NET API does not serve images.

export interface ItemImage {
  id: number;
  itemId: number;
  url: string;
  caption: string;
  isPrimary: boolean;
  // Position in the gallery, starting at 1.
  sortOrder: number;
}
