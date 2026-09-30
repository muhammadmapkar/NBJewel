// Role: Describes a product category as the .NET API returns it.

export interface Category {
  id: number;
  name: string;
  parentId: number | null;
}
