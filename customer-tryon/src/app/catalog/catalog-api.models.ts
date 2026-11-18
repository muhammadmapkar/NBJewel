// Role: Maps the narrow public API into existing shop/try-on contracts, preserving server
// prices and recorded dates. Invalid or unsupported data fails instead of inventing values.
import { Catalog, PieceImage, Product, PublicMetalRate } from './catalog.models';
import { isCameraTrimPercent } from '../tryon/necklace-crop';
export interface ApiPrice {
  itemId: number; currency: string; ratePerGram: number; metalValue: number; makingType: string; makingValue: number;
  makingCharge: number; stoneValue: number; vatPercent: number; vatAmount: number; totalPrice: number;
  effectiveDate: string; change?: { explanation: string } | null;
}
export interface ApiProduct {
  id: number; code: string; name: string; category: string; metalType: string; purity: string;
  grossWeight: number; netWeight: number; previewUrl: string; previewAspect: number; price?: ApiPrice | null;
  cameraTrimPercent?: number | null;
  storefront?: { description?: string; collection?: string; note?: string; occasions?: string[]; stones?: string[]; size?: string;
    badge?: 'new' | 'bestseller' | null; popularity?: number; addedOn?: string | null; isSample?: boolean } | null;
}
export interface ApiPage { items: ApiProduct[]; totalCount: number; page: number; pageSize: number; }
function date(value: string): boolean { return /^\d{4}-\d{2}-\d{2}$/.test(value) && Number.isFinite(Date.parse(value)) && new Date(value).toISOString().startsWith(value); }
function nonnegative(...values: number[]): boolean { return values.every(x => typeof x === 'number' && Number.isFinite(x) && x >= 0); }
export function catalogFromApi(rows: ApiProduct[], rates: PublicMetalRate[], base: string, options?: { pricesHidden: boolean; slug?: string; preview?: boolean }): Catalog {
  if (!Array.isArray(rates) || rates.some(r => !['Gold', 'Silver', 'Platinum'].includes(r.metalType) || !r.purity || !date(r.effectiveDate) || !nonnegative(r.ratePerGram))) throw new Error('Invalid rates.');
  const currency = rows[0]?.price?.currency ?? rates[0]?.currency ?? 'AED';
  if (!/^[A-Z]{3}$/.test(currency)) throw new Error('Invalid currency.');
  if (rates.some(r => r.currency !== currency)) throw new Error('Mixed catalog currencies.');
  const products: Product[] = rows.map(row => {
    const p = row.price;
    const hidden = options?.pricesHidden === true;
    const prefix = options?.slug ? options.preview ? `/api/businesses/${options.slug}/portal/products` : `/api/shops/${options.slug}/products` : '/api/storefront/products';
    if (!Number.isInteger(row.id) || row.id < 1 || !row.name || !row.code || !['necklace', 'earrings'].includes(row.category) ||
        !['Gold', 'Silver', 'Platinum'].includes(row.metalType) || !row.purity || !nonnegative(row.grossWeight, row.netWeight) ||
        !row.previewUrl?.startsWith(`${prefix}/${row.id}/preview?`) || !Number.isFinite(row.previewAspect) || row.previewAspect <= 0) throw new Error('Invalid public product.');
    if (hidden ? p != null : !p || p.itemId !== row.id || p.currency !== currency || !date(p.effectiveDate) || !['percent', 'perGram', 'flat'].includes(p.makingType) || p.vatPercent > 100 || !nonnegative(p.makingValue, p.ratePerGram, p.metalValue, p.makingCharge, p.stoneValue, p.vatPercent, p.vatAmount, p.totalPrice) || Math.abs(p.metalValue + p.makingCharge + p.stoneValue + p.vatAmount - p.totalPrice) > 0.02) throw new Error('Invalid public price policy.');
    const preview = new URL(base + row.previewUrl);
    if (preview.searchParams.get('v')?.match(/^[a-f0-9]{32}$/) === null || !preview.searchParams.has('v')) throw new Error('Invalid preview revision.');
    const image: PieceImage = { image: preview.href, aspect: row.previewAspect };
    if (row.category === 'necklace' && row.cameraTrimPercent != null) {
      if (!isCameraTrimPercent(row.cameraTrimPercent)) throw new Error('Invalid camera trim.');
      image.cameraTrimPercent = row.cameraTrimPercent;
    }
    const m = row.storefront;
    return {
      id: String(row.id), code: row.code, name: row.name, note: m?.note ?? 'Sample ERP item', description: m?.description ?? row.name,
      collection: m?.collection ?? 'Sample', occasions: m?.occasions ?? [], category: row.category as 'necklace' | 'earrings',
      kinds: [row.category as 'necklace' | 'earrings'], card: preview.href, metal: row.metalType, purity: row.purity,
      hallmark: 'Sample specifications; hallmark unverified', grossWeight: row.grossWeight, netWeight: row.netWeight,
      makingPercent: p?.makingType === 'percent' ? p.makingValue : 0,
      makingLabel: !p ? '' : p.makingType === 'percent' ? `${p.makingValue}% of metal value` : p.makingType === 'perGram' ? `${p.currency} ${p.makingValue} per gram` : `${p.currency} ${p.makingValue} fixed`,
      stones: m?.stones ?? [], stoneValue: p?.stoneValue ?? 0, size: m?.size ?? '', badge: m?.badge ?? null,
      popularity: m?.popularity ?? 0, addedOn: m?.addedOn ?? '',
      // Assumed: an approved earring image is one earring, used for both placements. Fit/quality needs manual review.
      parts: row.category === 'necklace' ? { necklace: image } : { earringLeft: image, earringRight: image },
      apiPrice: !p ? undefined : { gold: p.metalValue, making: p.makingCharge, stones: p.stoneValue, vat: p.vatAmount, total: p.totalPrice,
        rate: p.ratePerGram, vatPercent: p.vatPercent, effectiveDate: p.effectiveDate, changeExplanation: p.change?.explanation }
    };
  });
  if (new Set(products.map(p => p.id)).size !== products.length) throw new Error('Duplicate product IDs.');
  return { currency, rates: Object.fromEntries(rates.filter(r => r.metalType === 'Gold').map(r => [r.purity, r.ratePerGram])),
    metalRates: rates, ratesNote: 'Recorded sample metal rates; effective dates are supplied by the API.', vatPercent: 0,
    branches: [], sample: true, products };
}
