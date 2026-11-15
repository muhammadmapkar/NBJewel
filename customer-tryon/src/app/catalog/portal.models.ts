// Role: Defines the assumed fixed portal settings received from the current published business revision.
export interface PortalSettings {
  name: string; colorPreset: 'gold' | 'silver' | 'rose'; logoAssetId?: string | null; bannerAssetId?: string | null;
  headline: string; introduction: string; story: string; phone: string; whatsApp: string; email: string; openingHours: string;
  showBanner: boolean; showCollections: boolean; showFeatured: boolean; showStory: boolean; showStores: boolean; showMarketRates: boolean;
  showPrices: boolean; enquiries: boolean; tryOn: boolean; bag: boolean; reservations: boolean;
  visibleCollections: string[]; featuredItemIds: number[]; visibleBranchIds: number[];
}
export interface PublicPortal { slug: string; revision: number; settings: PortalSettings; }
export function checkedPortal(value: PublicPortal, slug: string): PortalSettings {
  const s = value?.settings;
  if (value?.slug !== slug || !Number.isSafeInteger(value.revision) || value.revision < 1 || !s || typeof s.name !== 'string' || !['gold', 'silver', 'rose'].includes(s.colorPreset) ||
      ['showBanner','showCollections','showFeatured','showStory','showStores','showMarketRates','showPrices','enquiries','tryOn','bag','reservations'].some(key => typeof s[key as keyof PortalSettings] !== 'boolean') ||
      !Array.isArray(s.featuredItemIds) || !Array.isArray(s.visibleBranchIds) || !Array.isArray(s.visibleCollections)) throw new Error('Invalid portal configuration.');
  return s;
}
